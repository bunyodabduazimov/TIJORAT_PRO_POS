using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using FFPOS.Models;
using FFPOS.Services;
using MaterialDesignThemes.Wpf;

namespace FFPOS.Views;

public partial class LoginWindow : Window
{
    private const double CompactLayoutWidth = 900;
    private AppActivationSettings _settings;
    private IReadOnlyList<User> _users = Array.Empty<User>();
    private bool _isPasswordVisible;
    private bool _isSyncingPassword;
    private bool _isCompactLayout;
    private bool _hasAppliedResponsiveLayout;
    private bool _suppressLoginDropDownOpen;

    public User? AuthenticatedUser { get; private set; }

    public LoginWindow(AppActivationSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        AppNameText.Text = string.IsNullOrWhiteSpace(settings.AppName)
            ? string.Empty
            : $"{settings.AppName} · {(settings.IsActivated ? "Активен" : "Отключен")}";
        UpdatePlaceholders();
        UpdateResponsiveLayout();
    }

    public string Login => GetLogin();

    private void LoginClicked(object sender, RoutedEventArgs e)
    {
        var selectedUser = LoginBox.SelectedItem as LoginUserOption;
        var enteredPin = GetPassword().Trim();

        if (selectedUser is null)
        {
            ErrorText.Text = "Выберите пользователя";
            return;
        }

        if (string.IsNullOrWhiteSpace(enteredPin))
        {
            ErrorText.Text = "Введите пинкод";
            return;
        }

        var user = _users.FirstOrDefault(item => item.Id == selectedUser.Id);
        if (user is null)
        {
            ErrorText.Text = "Пользователь не найден";
            return;
        }

        if (!string.Equals(user.Pincode?.Trim(), enteredPin, StringComparison.OrdinalIgnoreCase))
        {
            ErrorText.Text = "Неверный пинкод";
            return;
        }

        AuthenticatedUser = user;
        DialogResult = true;
        Close();
    }

    private void NumberKeyClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Content is null)
        {
            return;
        }

        SetPassword(GetPassword() + button.Content);
    }

    private void ClearPasswordClicked(object sender, RoutedEventArgs e)
    {
        SetPassword(string.Empty);
    }

    private void BackspaceClicked(object sender, RoutedEventArgs e)
    {
        var password = GetPassword();
        if (password.Length > 0)
        {
            SetPassword(password[..^1]);
        }
    }

    private void TogglePasswordClicked(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;

        if (_isPasswordVisible)
        {
            VisiblePasswordBox.Text = PasswordBox.Password;
            PasswordBox.Visibility = Visibility.Collapsed;
            VisiblePasswordBox.Visibility = Visibility.Visible;
            VisiblePasswordBox.Focus();
            VisiblePasswordBox.CaretIndex = VisiblePasswordBox.Text.Length;
            PasswordEyeIcon.Kind = PackIconKind.EyeOutline;
            UpdatePlaceholders();
            return;
        }

        PasswordBox.Password = VisiblePasswordBox.Text;
        VisiblePasswordBox.Visibility = Visibility.Collapsed;
        PasswordBox.Visibility = Visibility.Visible;
        PasswordBox.Focus();
        PasswordEyeIcon.Kind = PackIconKind.EyeOffOutline;
        UpdatePlaceholders();
    }

    private void PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncingPassword)
        {
            return;
        }

        _isSyncingPassword = true;
        VisiblePasswordBox.Text = PasswordBox.Password;
        _isSyncingPassword = false;
        UpdatePlaceholders();
    }

    private void VisiblePasswordChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncingPassword)
        {
            return;
        }

        _isSyncingPassword = true;
        PasswordBox.Password = VisiblePasswordBox.Text;
        _isSyncingPassword = false;
        VisiblePasswordBox.CaretIndex = VisiblePasswordBox.Text.Length;
        VisiblePasswordBox.SelectionStart = VisiblePasswordBox.CaretIndex;
        VisiblePasswordBox.SelectionLength = 0;
        UpdatePlaceholders();
    }

    private void LoginSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdatePlaceholders();

        if (LoginBox.SelectedItem is not null)
        {
            _suppressLoginDropDownOpen = true;
            Dispatcher.BeginInvoke(() => LoginBox.IsDropDownOpen = false, DispatcherPriority.Input);
            Dispatcher.BeginInvoke(() => _suppressLoginDropDownOpen = false, DispatcherPriority.Background);
        }
    }

    private void OpenSyncClicked(object sender, RoutedEventArgs e)
    {
        var window = new SyncWindow(_settings)
        {
            Owner = this
        };
        window.ShowDialog();
    }

    private void OpenKeyboardClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "osk.exe",
                UseShellExecute = true
            });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "tabtip.exe",
                    UseShellExecute = true
                });
            }
            catch
            {
                ErrorText.Text = "Не удалось открыть экранную клавиатуру";
            }
        }
    }

    private void OpenSettingsClicked(object sender, RoutedEventArgs e)
    {
        var codeWindow = new AdminCodeWindow(_settings.AdminCode)
        {
            Owner = this
        };

        if (codeWindow.ShowDialog() != true)
        {
            return;
        }

        var settingsWindow = new SettingsWindow
        {
            Owner = this
        };
        settingsWindow.ShowDialog();

        if (settingsWindow.WasReset)
        {
            RunActivationAfterReset();
        }
    }

    private async void WindowLoaded(object sender, RoutedEventArgs e)
    {
        await LoadUsersAsync();
    }

    private void RunActivationAfterReset()
    {
        var settingsService = new AppSettingsService();
        _settings = settingsService.Load();
        SetPassword(string.Empty);
        LoginBox.SelectedIndex = -1;
        LoginBox.Text = string.Empty;
        UpdatePlaceholders();

        var activationWindow = new PhoneActivationWindow(_settings)
        {
            Owner = this
        };

        if (activationWindow.ShowDialog() == true && activationWindow.ActivatedApp is not null)
        {
            _settings.ApplyApp(activationWindow.ActivatedApp);
            settingsService.Save(_settings);
            AppNameText.Text = string.IsNullOrWhiteSpace(_settings.AppName)
                ? string.Empty
                : $"{_settings.AppName} · {(_settings.IsActivated ? "Активен" : "Отключен")}";
        }
    }

    private void LoginBoxPreviewGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_suppressLoginDropDownOpen)
        {
            return;
        }

        LoginBox.IsDropDownOpen = true;
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !IsInsideButton(e.OriginalSource as DependencyObject))
        {
            DragMove();
        }
    }

    private string GetPassword()
    {
        return _isPasswordVisible ? VisiblePasswordBox.Text : PasswordBox.Password;
    }

    private string GetLogin()
    {
        return LoginBox.SelectedItem is LoginUserOption item
            ? item.DisplayName.Trim()
            : LoginBox.Text.Trim();
    }

    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            LoginClicked(sender, e);
        }
    }

    private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout();
    }

    private void UpdateResponsiveLayout()
    {
        var useCompact = ActualWidth > 0 && ActualWidth < CompactLayoutWidth;
        if (_hasAppliedResponsiveLayout && useCompact == _isCompactLayout)
        {
            return;
        }

        _isCompactLayout = useCompact;
        _hasAppliedResponsiveLayout = true;

        if (useCompact)
        {
            FormColumn.Width = new GridLength(1, GridUnitType.Star);
            SpacerColumn.Width = new GridLength(0);
            KeypadColumn.Width = new GridLength(0);
            FormColumn.MinWidth = 0;
            SpacerColumn.MinWidth = 0;
            KeypadColumn.MinWidth = 0;

            Grid.SetColumn(FormPanel, 0);
            Grid.SetRow(FormPanel, 0);
            Grid.SetColumn(KeypadPanel, 0);
            Grid.SetRow(KeypadPanel, 2);
            FormPanel.Width = 450;
            FormPanel.MaxWidth = 450;
            FormPanel.HorizontalAlignment = HorizontalAlignment.Center;
            KeypadPanel.HorizontalAlignment = HorizontalAlignment.Center;
            return;
        }

        FormColumn.Width = new GridLength(450);
        SpacerColumn.Width = new GridLength(1, GridUnitType.Star);
        KeypadColumn.Width = new GridLength(456);
        FormColumn.MinWidth = 360;
        SpacerColumn.MinWidth = 22;
        KeypadColumn.MinWidth = 330;

        Grid.SetColumn(FormPanel, 0);
        Grid.SetRow(FormPanel, 0);
        Grid.SetColumn(KeypadPanel, 2);
        Grid.SetRow(KeypadPanel, 0);
        FormPanel.Width = 450;
        FormPanel.MaxWidth = 450;
        FormPanel.HorizontalAlignment = HorizontalAlignment.Left;
        KeypadPanel.HorizontalAlignment = HorizontalAlignment.Right;
    }

    private void SetPassword(string password)
    {
        _isSyncingPassword = true;
        PasswordBox.Password = password;
        VisiblePasswordBox.Text = password;
        _isSyncingPassword = false;

        if (_isPasswordVisible)
        {
            VisiblePasswordBox.Focus();
            VisiblePasswordBox.CaretIndex = VisiblePasswordBox.Text.Length;
            VisiblePasswordBox.SelectionStart = VisiblePasswordBox.CaretIndex;
            VisiblePasswordBox.SelectionLength = 0;
        }
        else
        {
            PasswordBox.Focus();
        }

        UpdatePlaceholders();
    }

    private void UpdatePlaceholders()
    {
        LoginPlaceholder.Visibility = LoginBox.SelectedItem is null
            ? Visibility.Visible
            : Visibility.Collapsed;

        PasswordPlaceholder.Visibility = string.IsNullOrEmpty(GetPassword())
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static bool IsInsideButton(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is Button)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            var databaseService = new DatabaseService();
            _users = await databaseService.GetUsersAsync();
        }
        catch (Exception ex)
        {
            _users = Array.Empty<User>();
            ErrorText.Text = "\u041d\u0435 \u0443\u0434\u0430\u043b\u043e\u0441\u044c \u043e\u0442\u043a\u0440\u044b\u0442\u044c \u0431\u0430\u0437\u0443 \u0434\u0430\u043d\u043d\u044b\u0445. \u041f\u0440\u043e\u0432\u0435\u0440\u044c\u0442\u0435 \u043f\u043e\u0434\u043a\u043b\u044e\u0447\u0435\u043d\u0438\u0435 \u0432 \u043d\u0430\u0441\u0442\u0440\u043e\u0439\u043a\u0430\u0445.";
            ErrorText.ToolTip = ex.Message;
        }

        var loginUsers = _users
            .Select(user => new LoginUserOption
            {
                Id = user.Id,
                DisplayName = string.IsNullOrWhiteSpace(user.Name) ? (user.Username ?? $"\u041f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u044c {user.Id}") : user.Name,
                Username = user.Username ?? string.Empty
            })
            .ToList();

        var selectedLogin = GetLogin();
        LoginBox.ItemsSource = loginUsers;
        ErrorText.Text = loginUsers.Count == 0 && string.IsNullOrWhiteSpace(ErrorText.Text)
            ? "\u041f\u043e\u043b\u044c\u0437\u043e\u0432\u0430\u0442\u0435\u043b\u0438 \u043d\u0435 \u043d\u0430\u0439\u0434\u0435\u043d\u044b. \u0412\u044b\u043f\u043e\u043b\u043d\u0438\u0442\u0435 \u0441\u0438\u043d\u0445\u0440\u043e\u043d\u0438\u0437\u0430\u0446\u0438\u044e."
            : ErrorText.Text;

        if (!string.IsNullOrWhiteSpace(selectedLogin))
        {
            var selected = loginUsers.FirstOrDefault(item =>
                string.Equals(item.DisplayName, selectedLogin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.Username, selectedLogin, StringComparison.OrdinalIgnoreCase));

            if (selected is not null)
            {
                LoginBox.SelectedItem = selected;
            }
        }
    }

    private sealed class LoginUserOption
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public override string ToString() => DisplayName;
    }
}
