using System.Windows;

namespace AgentDesk.Spike;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        MainWindow = window;
        if (e.Args.Contains("--smoke"))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            window.ContentRendered += async (_, _) => await SmokeTest.RunAsync(window);
        }
        window.Show();
    }
}
