using System.Windows;
using FileVault.UI.Services;

namespace FileVault.UI;

public partial class App : Application
{
    public static new MainWindow MainWindow { get; private set; } = null!;
    private SystemTrayService? _tray;
    private bool _exitRequested;

    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            Logger.Log("UnhandledException", e.Exception);
            e.Handled = true;
        };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MainWindow = new MainWindow();
        ((Application)this).MainWindow = MainWindow;

        _tray = new SystemTrayService();
        _tray.ShowWindowRequested += ShowMainWindow;
        _tray.ExitRequested += ExitApp;
        _tray.Show();

        // Intercept window close → hide to tray instead of exiting
        MainWindow.Closing += (_, closeArgs) =>
        {
            if (!_exitRequested)
            {
                closeArgs.Cancel = true;
                MainWindow.Hide();
            }
        };

        MainWindow.Show();
    }

    private void ShowMainWindow()
    {
        MainWindow.Show();
        if (MainWindow.WindowState == WindowState.Minimized)
            MainWindow.WindowState = WindowState.Normal;
        MainWindow.Activate();
    }

    private void ExitApp()
    {
        _exitRequested = true;
        _tray?.Dispose();
        Shutdown();
    }
}
