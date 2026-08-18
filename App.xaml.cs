using System.Windows;
using FFPOS.Models;
using FFPOS.Services;
using FFPOS.Views;

namespace FFPOS;

public partial class App : Application
{
    public static User? CurrentUser { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var settingsService = new AppSettingsService();
        settingsService.EnsureCreated();
        var settings = settingsService.Load();
        if (!TryInitializeDatabase(settingsService, settings))
        {
            Shutdown();
            return;
        }

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

        CurrentUser = loginWindow.AuthenticatedUser;

        settings = settingsService.Load();
        var mainWindow = CreateMainWindow(settings);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    public static Window CreateMainWindow(AppActivationSettings settings)
    {
        return new MainWindow(settings);
    }

    public static void SwitchMainWindow(AppActivationSettings settings)
    {
        if (Current is not App app)
        {
            return;
        }

        var previousWindow = app.MainWindow;
        var nextWindow = CreateMainWindow(settings);

        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.MainWindow = nextWindow;
        nextWindow.Show();

        if (previousWindow is not null && previousWindow != nextWindow)
        {
            previousWindow.Close();
        }

        app.ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    public static void ShowLoginWindow()
    {
        if (Current is not App app)
        {
            return;
        }

        var settingsService = new AppSettingsService();
        var settings = settingsService.Load();
        var previousWindow = app.MainWindow;
        var loginWindow = new LoginWindow(settings);

        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.MainWindow = loginWindow;

        if (previousWindow is not null && previousWindow != loginWindow)
        {
            previousWindow.Close();
        }

        if (loginWindow.ShowDialog() == true)
        {
            CurrentUser = loginWindow.AuthenticatedUser;
            settings = settingsService.Load();
            var nextWindow = CreateMainWindow(settings);
            app.MainWindow = nextWindow;
            nextWindow.Show();

            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            return;
        }

        app.Shutdown();
    }

    private static bool TryInitializeDatabase(AppSettingsService settingsService, AppActivationSettings settings)
    {
        try
        {
            var databaseService = new DatabaseService();
            databaseService.InitializeAsync().GetAwaiter().GetResult();
            return true;
        }
        catch (Exception) when (settings.DatabaseType == 2)
        {
            settings.DatabaseType = 1;
            settingsService.Save(settings);

            try
            {
                var databaseService = new DatabaseService();
                databaseService.InitializeAsync().GetAwaiter().GetResult();
                AppDialogWindow.ShowError(
                    "Не удалось подключиться к MySQL. Программа временно переключена на локальную базу SQLite3. Проверьте параметры подключения в настройках.",
                    "База данных недоступна");
                return true;
            }
            catch (Exception fallbackEx)
            {
                AppDialogWindow.ShowError(
                    $"Не удалось открыть локальную базу данных.\n{fallbackEx.Message}",
                    "Ошибка базы данных");
                return false;
            }
        }
        catch (Exception ex)
        {
            AppDialogWindow.ShowError(
                $"Не удалось открыть базу данных.\n{ex.Message}",
                "Ошибка базы данных");
            return false;
        }
    }
}
