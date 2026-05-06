using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Clipt.App.ViewModels;

namespace Clipt.App.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = ViewModel;
        ViewModel.CloseRequested += (_, _) => Close();
    }

    public SettingsViewModel ViewModel { get; }

    public Task ReloadAsync(CancellationToken cancellationToken) => ViewModel.LoadAsync(cancellationToken);
}
