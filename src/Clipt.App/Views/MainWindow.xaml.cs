using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Clipt.App.ViewModels;
using Clipt.Interop;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Clipt.App.Views;

public partial class MainWindow : Window
{
    private const double CaptureModeWidth = 380;
    private const double WorkModeWidth = 880;
    private const int DwmBackdropMica = 2;
    private const int DwmCornerPreferenceRound = 2;
    private readonly ILogger<MainWindow> _logger;
    private readonly MainViewModel _viewModel;
    private bool _isQuitting;

    public MainWindow(MainViewModel viewModel, ILogger<MainWindow> logger)
    {
        _viewModel = viewModel;
        _logger = logger;
        ShowFromTrayCommand = new RelayCommand(ShowFromTray);

        InitializeComponent();
        DataContext = _viewModel;
        UpdateModeLayout(animate: false);
    }

    public ICommand ShowFromTrayCommand { get; }

    private async void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync(CancellationToken.None);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        ApplyWindowsBackdrop();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Tab)
        {
            ToggleMode();
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

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isQuitting)
        {
            TrayIcon.Dispose();
            return;
        }

        e.Cancel = true;
        Hide();
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

    private void OnQuitTrayClick(object sender, RoutedEventArgs e)
    {
        _isQuitting = true;
        Close();
        Application.Current.Shutdown();
    }

    private void ToggleMode()
    {
        _viewModel.ToggleModeCommand.Execute(null);
        UpdateModeLayout(animate: true);
    }

    private void UpdateModeLayout(bool animate)
    {
        var targetWidth = _viewModel.IsWorkMode ? WorkModeWidth : CaptureModeWidth;
        PreviewColumn.Width = _viewModel.IsWorkMode ? new GridLength(1.35, GridUnitType.Star) : new GridLength(0);
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

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
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
