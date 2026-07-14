using System.Windows;

namespace ClaudeCosts.App;

/// <summary>
/// The dashboard window. Its <c>DataContext</c> (a <see cref="ViewModels.MainViewModel"/>)
/// is assigned by <see cref="App"/>; closing it only hides the window — the app lives in the tray.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
