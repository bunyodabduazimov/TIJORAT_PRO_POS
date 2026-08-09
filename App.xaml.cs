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
