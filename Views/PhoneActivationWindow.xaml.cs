using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FFPOS.Models;
using FFPOS.Services;

namespace FFPOS.Views;

public partial class PhoneActivationWindow : Window
{
    private readonly AppActivationSettings _settings;
    private readonly AuthApiClient _authApiClient;

    public PhoneActivationWindow(AppActivationSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _authApiClient = new AuthApiClient(settings.EffectiveApiBaseUrl);
    }

    public AppInfo? ActivatedApp { get; private set; }

    private async void SendClicked(object sender, RoutedEventArgs e)
    {
        var phone = PhoneBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(phone))
        {
            ErrorText.Text = "Телефон обязателен";
            return;
        }

        SetLoading(true);
        var response = await _authApiClient.SendCodeAsync(phone);
        SetLoading(false);

        if (!response.IsSuccess)
        {
            ErrorText.Text = response.Message;
            return;
        }

        var codeWindow = new CodeVerificationWindow(_settings, phone, response.ExpiresIn ?? 300)
        {
            Owner = this
        };

        if (codeWindow.ShowDialog() == true)
        {
            ActivatedApp = codeWindow.ActivatedApp;
            DialogResult = true;
            Close();
        }
    }

    private void SetLoading(bool isLoading)
    {
        SendButton.IsEnabled = !isLoading;
        SendButton.Opacity = isLoading ? 0.72 : 1;
        ErrorText.Text = isLoading ? "Отправляем код..." : string.Empty;
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && !IsInsideButton(e.OriginalSource as DependencyObject))
        {
            DragMove();
        }
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
}
