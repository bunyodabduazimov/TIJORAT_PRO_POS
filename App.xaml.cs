using System.Windows;
using FFPOS.Models;
using FFPOS.Services;
using FFPOS.Views;

namespace FFPOS;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settingsService = new AppSettingsService();
        settingsService.EnsureCreated();
        var settings = settingsService.Load();
        var databaseService = new DatabaseService();
        databaseService.InitializeAsync().GetAwaiter().GetResult();

        if (!settings.IsActivated)
        {
            var activationWindow = new PhoneActivationWindow(settings);
            if (activationWindow.ShowDialog() != true || activationWindow.ActivatedApp is null)
            {
                Shutdown();
                return;
            }

            settings.ApplyApp(activationWindow.ActivatedApp);
            settingsService.Save(settings);
        }

        var loginWindow = new LoginWindow(settings);
        if (loginWindow.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        var mainWindow = CreateMainWindow(settings);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    private static Window CreateMainWindow(AppActivationSettings settings)
    {
        return settings.AppType switch
        {
            2 => new PharmacyWindow(),
            3 => new FastFoodWindow(),
            _ => new MainWindow()
        };
    }
}
