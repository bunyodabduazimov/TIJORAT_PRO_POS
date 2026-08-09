using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FFPOS.Models;
using FFPOS.Services;

namespace FFPOS.Views;

public partial class CodeVerificationWindow : Window
{
    private readonly AppActivationSettings _settings;
    private readonly AuthApiClient _authApiClient;
    private readonly string _phone;
    private bool _isUpdatingCode;

    public CodeVerificationWindow(AppActivationSettings settings, string phone, int expiresIn)
    {
        InitializeComponent();

        _settings = settings;
        _authApiClient = new AuthApiClient(settings.EffectiveApiBaseUrl);
        _phone = phone;
        SentText.Text = $"Код отправлен на {_phone}";
        ExpiresText.Text = $"Срок действия: {expiresIn / 60:0} мин.";
        Code1.Focus();
    }

    public AppInfo? ActivatedApp { get; private set; }

    private async void VerifyClicked(object sender, RoutedEventArgs e)
    {
        await VerifyAsync();
    }

    private async Task VerifyAsync()
    {
        var code = GetCode();
        if (code.Length != 4)
        {
            ErrorText.Text = "Введите 4 цифры кода";
            return;
        }

        SetLoading(true);
        var response = await _authApiClient.VerifyCodeAsync(_phone, code);
        SetLoading(false);

        if (!response.IsSuccess || response.App is null)
        {
            ErrorText.Text = response.Message;
            return;
        }

        ActivatedApp = response.App.ToAppInfo();
        DialogResult = true;
        Close();
    }

    private async void CodeTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingCode || sender is not TextBox textBox)
        {
            return;
        }

        _isUpdatingCode = true;
        if (textBox.Text.Length > 1)
        {
            textBox.Text = textBox.Text[^1].ToString();
            textBox.CaretIndex = textBox.Text.Length;
        }

        if (!string.IsNullOrWhiteSpace(textBox.Text))
        {
            MoveNext(textBox);
        }

        _isUpdatingCode = false;

        if (GetCode().Length == 4)
        {
            await VerifyAsync();
        }
    }

    private void CodePreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(ch => !char.IsDigit(ch));
    }

    private void CodePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Back || sender is not TextBox textBox || !string.IsNullOrEmpty(textBox.Text))
        {
            return;
        }

        MovePrevious(textBox);
    }

    private string GetCode()
    {
        return $"{Code1.Text}{Code2.Text}{Code3.Text}{Code4.Text}";
    }

    private void MoveNext(TextBox textBox)
    {
        if (textBox == Code1) Code2.Focus();
        else if (textBox == Code2) Code3.Focus();
        else if (textBox == Code3) Code4.Focus();
    }

    private void MovePrevious(TextBox textBox)
    {
        if (textBox == Code4) Code3.Focus();
        else if (textBox == Code3) Code2.Focus();
        else if (textBox == Code2) Code1.Focus();
    }

    private void SetLoading(bool isLoading)
    {
        VerifyButton.IsEnabled = !isLoading;
        VerifyButton.Opacity = isLoading ? 0.72 : 1;
        ErrorText.Text = isLoading ? "Проверяем код..." : string.Empty;
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
