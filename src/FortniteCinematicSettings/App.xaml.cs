using System.Configuration;
using System.Data;
using System.Windows;

namespace FortniteCinematicSettings;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (MainWindow is null) new MainWindow().Show();
    }
}

