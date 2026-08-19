using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FFPOS.Models;
using FFPOS.Services;

namespace FFPOS.Views;

public partial class ActivationExpiredWindow : Window
{
    private readonly AppSettingsService _settingsService = new();
    private AppActivationSettings _settings;

    public ActivationExpiredWindow(AppActivationSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        MessageText.Text = $"Срок активации истек: {FormatDate(_settings.AppDate)}. Обновите статус с сервера, чтобы продолжить работу.";
    }

    public AppActivationSettings Settings => _settings;

    private async void RefreshClicked(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(102, 112, 133));
        StatusText.Text = "Проверяем статус на сервере...";

        try
        {
            var response = await new AuthApiClient(_settings.EffectiveApiBaseUrl).CheckStatusAsync(_settings);
            if (!response.IsSuccess || response.App is null)
            {
                ShowError(string.IsNullOrWhiteSpace(response.Message) ? "Сервер не вернул активный статус." : response.Message);
                return;
            }

            var publicUrl = _settings.PublicUrl;
            _settings.ApplyApp(response.App.ToAppInfo());
            _settings.PublicUrl = publicUrl;

            if (!_settings.IsActivated || IsActivationExpired(_settings.AppDate))
            {
                ShowError($"Активация всё ещё истекла: {FormatDate(_settings.AppDate)}");
                return;
            }

            _settingsService.Save(_settings);
            DialogResult = true;
            Close();
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    public static bool IsActivationExpired(string? value)
    {
        return TryParseActivationDate(value, out var date) && date.Date < DateTime.Today;
    }

    private static bool TryParseActivationDate(string? value, out DateTime date)
    {
        if (DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out date))
        {
            return true;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date);
    }

    private static string FormatDate(string? value)
    {
        return TryParseActivationDate(value, out var date)
            ? date.ToString("dd.MM.yyyy")
            : string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private void ShowError(string message)
    {
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(249, 31, 37));
        StatusText.Text = message;
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
