using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Clipt.App.Services;
using Clipt.App.ViewModels;
using Clipt.Core.Models;
using Clipt.Core.Services;
using Clipt.Interop;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Views;

public partial class MainWindow : Window
{
    private const int DwmBackdropMica = 2;
    private const int DwmCornerPreferenceRound = 2;
    private const int SaveDebounceMs = 500;

    private readonly ILogger<MainWindow> _logger;
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly IHotkeyService _hotkeyService;
    private readonly ForegroundWindowTracker _foregroundTracker;
    private bool _isQuitting;
    private bool _isInitialized;

    private DateTime _lastSaveTime = DateTime.MinValue;
    private AppSettings _pendingSave = new();

    public MainWindow(
        MainViewModel viewModel,
        ISettingsService settingsService,
        IHotkeyService hotkeyService,
        ForegroundWindowTracker foregroundTracker,
        ILogger<MainWindow> logger)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _foregroundTracker = foregroundTracker;
        _logger = logger;
        ShowFromTrayCommand = new RelayCommand(ShowFromTray);

        InitializeComponent();
        DataContext = _viewModel;

        SizeChanged += OnWindowSizeChanged;
        LocationChanged += OnWindowLocationChanged;

        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
    }

    public ICommand ShowFromTrayCommand { get; }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await LoadAndApplySettingsAsync();

        _isInitialized = true;
        UpdateModeLayout(animate: false);

        await _viewModel.InitializeAsync(CancellationToken.None);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyWindowsBackdrop();
    }

    private async void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            await SaveAndHideAsync();
            return;
        }

        if (e.Key == Key.Tab)
        {
            ToggleMode();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (_viewModel.SelectedItem is not null)
            {
                _viewModel.TogglePinCommand.Execute(_viewModel.SelectedItem);
            }

            e.Handled = true;
        }
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void OnToggleModeClick(object sender, RoutedEventArgs e)
    {
        ToggleMode();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private async void OnCloseClick(object sender, RoutedEventArgs e)
    {
        await SaveAndHideAsync();
    }

    private async void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isQuitting)
        {
            // Quit path: settings already saved and tray disposed by OnQuitTrayClick.
            return;
        }

        e.Cancel = true;
        await SaveAndHideAsync();
    }

    private void OnOpacityValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized)
        {
            return;
        }

        Opacity = e.NewValue;
        DebounceSave();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_isInitialized || WindowState != WindowState.Normal)
        {
            return;
        }

        DebounceSave();
    }

    private void OnWindowLocationChanged(object? sender, EventArgs e)
    {
        if (!_isInitialized || WindowState != WindowState.Normal)
        {
            return;
        }

        DebounceSave();
    }

    private void OnOpenTrayClick(object sender, RoutedEventArgs e)
    {
        ShowFromTray();
    }

    private void OnSettingsTrayClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(this, "Settings arrive in Phase 2.", "Clipt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnAboutTrayClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            this,
            "Clipt Phase 1\nVisual foundation preview",
            "About Clipt",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private async void OnQuitTrayClick(object sender, RoutedEventArgs e)
    {
        _isQuitting = true;
        await SaveCurrentSettingsAsync();
        TrayIcon.Dispose();
        Close();
        Application.Current.Shutdown();
    }

    private async void ToggleMode()
    {
        _viewModel.ToggleModeCommand.Execute(null);
        UpdateModeLayout(animate: true);
        await SaveModeOnlyAsync();
    }

    private void UpdateModeLayout(bool animate)
    {
        var targetWidth = _viewModel.IsWorkMode
            ? _pendingSave.WorkModeWidth
            : _pendingSave.CaptureModeWidth;

        PreviewColumn.Width = _viewModel.IsWorkMode
            ? new GridLength(1.35, GridUnitType.Star)
            : new GridLength(0);
        PreviewColumn.MinWidth = _viewModel.IsWorkMode ? 430 : 0;

        if (!animate)
        {
            Width = targetWidth;
            return;
        }

        var animation = new DoubleAnimation
        {
            To = targetWidth,
            Duration = TimeSpan.FromMilliseconds(180),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        BeginAnimation(WidthProperty, animation, HandoffBehavior.SnapshotAndReplace);
    }

    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        // Must dispatch to the UI thread because the hotkey fires on a
        // background thread.
        Dispatcher.Invoke(() =>
        {
            if (_isQuitting)
            {
                return;
            }

            if (IsVisible && IsActive)
            {
                _ = SaveAndHideAsync();
            }
            else
            {
                ShowFromHotkey();
            }
        });
    }

    private void ShowFromTray()
    {
        _foregroundTracker.Capture();
        ShowFromHotkey();
    }

    private void ShowFromHotkey()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        FocusSearchBox();

        // Discard the captured HWND when it is zero or Clipt's own handle
        // so that future auto-paste only sees a genuine target window.
        var myHwnd = new WindowInteropHelper(this).Handle;
        if (_foregroundTracker.PreviousForegroundWindow == myHwnd
            || _foregroundTracker.PreviousForegroundWindow == 0)
        {
            _foregroundTracker.Clear();
        }
    }

    private void FocusSearchBox()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private async Task SaveAndHideAsync()
    {
        await SaveCurrentSettingsAsync();
        Hide();
    }

    private async Task LoadAndApplySettingsAsync()
    {
        try
        {
            var settings = await _settingsService.GetAsync(CancellationToken.None);
            _pendingSave = settings;

            _viewModel.IsWorkMode = settings.IsWorkMode;
            Width = settings.IsWorkMode ? settings.WorkModeWidth : settings.CaptureModeWidth;
            Height = settings.Height;
            Topmost = settings.AlwaysOnTop;
            Opacity = settings.Normalize().Opacity;
            TransparencyBar.Value = Opacity;

            if (settings.Left.HasValue && settings.Top.HasValue)
            {
                if (IsPositionOnScreen(settings.Left.Value, settings.Top.Value, Width, Height))
                {
                    Left = settings.Left.Value;
                    Top = settings.Top.Value;
                }
                else
                {
                    _logger.LogDebug(
                        "Saved window position ({Left}, {Top}) is off-screen, using default position.",
                        settings.Left, settings.Top);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to load settings, using defaults.");
        }
    }

    private async Task SaveCurrentSettingsAsync()
    {
        try
        {
            var isValid = WindowState == WindowState.Normal && IsLoaded;
            var left = isValid ? (int?)Math.Round(Left) : _pendingSave.Left;
            var top = isValid ? (int?)Math.Round(Top) : _pendingSave.Top;
            var width = isValid ? (int)Math.Round(ActualWidth) : (int)Width;
            var height = isValid ? (int)Math.Round(ActualHeight) : (int)Height;

            var settings = new AppSettings
            {
                IsWorkMode = _viewModel.IsWorkMode,
                Opacity = Opacity,
                CaptureModeWidth = _viewModel.IsWorkMode ? _pendingSave.CaptureModeWidth : width,
                WorkModeWidth = _viewModel.IsWorkMode ? width : _pendingSave.WorkModeWidth,
                Height = height,
                Left = left,
                Top = top,
                AlwaysOnTop = Topmost,
                OpenHotkey = _pendingSave.OpenHotkey,
                Theme = _pendingSave.Theme,
                AccentColor = _pendingSave.AccentColor,
            };

            _pendingSave = settings;
            await _settingsService.SaveAsync(settings, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to save settings.");
        }
    }

    private async Task SaveModeOnlyAsync()
    {
        try
        {
            _pendingSave = _pendingSave with { IsWorkMode = _viewModel.IsWorkMode };
            await _settingsService.SaveAsync(_pendingSave, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to save mode setting.");
        }
    }

    private async void DebounceSave()
    {
        var now = DateTime.UtcNow;
        _lastSaveTime = now;

        await Task.Delay(SaveDebounceMs);

        if (_lastSaveTime == now)
        {
            await SaveCurrentSettingsAsync();
        }
    }

    private static bool IsPositionOnScreen(double left, double top, double width, double height)
    {
        var centerX = left + width / 2;
        var centerY = top + height / 2;

        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        return centerX >= virtualLeft && centerX <= virtualRight
            && centerY >= virtualTop && centerY <= virtualBottom;
    }

    private void ApplyWindowsBackdrop()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            _logger.LogInformation("Mica backdrop is not available on this Windows version.");
            return;
        }

        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == 0)
            {
                _logger.LogDebug("Skipping DWM backdrop because the window handle is not initialized.");
                return;
            }

            SetDwmAttribute(hwnd, NativeMethods.DwmwaWindowCornerPreference, DwmCornerPreferenceRound);
            SetDwmAttribute(hwnd, NativeMethods.DwmwaSystemBackdropType, DwmBackdropMica);
        }
        catch (DllNotFoundException exception)
        {
            _logger.LogDebug(exception, "DWM API is unavailable; using the solid dark fallback.");
        }
        catch (EntryPointNotFoundException exception)
        {
            _logger.LogDebug(exception, "DWM backdrop entry point is unavailable; using the solid dark fallback.");
        }
        catch (ExternalException exception)
        {
            _logger.LogDebug(exception, "DWM backdrop setup failed; using the solid dark fallback.");
        }
    }

    private void SetDwmAttribute(nint hwnd, int attribute, int value)
    {
        var size = Marshal.SizeOf<int>();
        var result = NativeMethods.DwmSetWindowAttribute(hwnd, attribute, ref value, size);
        if (result != 0)
        {
            _logger.LogDebug("DwmSetWindowAttribute({Attribute}) returned {Result}.", attribute, result);
        }
    }
}
