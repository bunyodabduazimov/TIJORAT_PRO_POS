using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using FFPOS.Models;
using FFPOS.Services;
using FFPOS.Views;

namespace FFPOS;

public partial class App : Application
{
    private const string SingleInstanceMutexName = @"Global\TIJORAT_PRO_POS_SINGLE_INSTANCE";
    private const double TouchScrollThreshold = 6;
    private static readonly Dictionary<ScrollViewer, TouchScrollState> TouchScrollStates = new();
    private Mutex? _singleInstanceMutex;

    public static User? CurrentUser { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        RegisterTouchScrolling();

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

    protected override void OnExit(ExitEventArgs e)
    {
        TouchScrollStates.Clear();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        _singleInstanceMutex = null;
        base.OnExit(e);
    }

    private static void RegisterTouchScrolling()
    {
        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            UIElement.PreviewTouchDownEvent,
            new EventHandler<TouchEventArgs>(ScrollViewerPreviewTouchDown),
            true);
        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            UIElement.PreviewTouchMoveEvent,
            new EventHandler<TouchEventArgs>(ScrollViewerPreviewTouchMove),
            true);
        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            UIElement.PreviewTouchUpEvent,
            new EventHandler<TouchEventArgs>(ScrollViewerPreviewTouchUp),
            true);
    }

    private static void ScrollViewerPreviewTouchDown(object? sender, TouchEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || IsInsideInteractiveControl(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var position = e.GetTouchPoint(scrollViewer).Position;
        TouchScrollStates[scrollViewer] = new TouchScrollState(position, scrollViewer.HorizontalOffset, scrollViewer.VerticalOffset);
    }

    private static void ScrollViewerPreviewTouchMove(object? sender, TouchEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || !TouchScrollStates.TryGetValue(scrollViewer, out var state))
        {
            return;
        }

        var position = e.GetTouchPoint(scrollViewer).Position;
        var deltaX = state.Start.X - position.X;
        var deltaY = state.Start.Y - position.Y;

        if (!state.IsDragging && Math.Abs(deltaX) < TouchScrollThreshold && Math.Abs(deltaY) < TouchScrollThreshold)
        {
            return;
        }

        state.IsDragging = true;
        TouchScrollStates[scrollViewer] = state;

        scrollViewer.ScrollToHorizontalOffset(state.HorizontalOffset + deltaX);
        scrollViewer.ScrollToVerticalOffset(state.VerticalOffset + deltaY);
        e.Handled = true;
    }

    private static void ScrollViewerPreviewTouchUp(object? sender, TouchEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            TouchScrollStates.Remove(scrollViewer);
        }
    }

    private static bool IsInsideInteractiveControl(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or TextBoxBase or PasswordBox or ComboBox)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private struct TouchScrollState
    {
        public TouchScrollState(Point start, double horizontalOffset, double verticalOffset)
        {
            Start = start;
            HorizontalOffset = horizontalOffset;
            VerticalOffset = verticalOffset;
            IsDragging = false;
        }

        public Point Start { get; }
        public double HorizontalOffset { get; }
        public double VerticalOffset { get; }
        public bool IsDragging { get; set; }
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
