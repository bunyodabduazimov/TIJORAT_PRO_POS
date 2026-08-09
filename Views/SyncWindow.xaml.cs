using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FFPOS.Models;
using FFPOS.Services;

namespace FFPOS.Views;

public partial class SyncWindow : Window
{
    private readonly SyncService _syncService;
    private string _lastErrorText = string.Empty;

    public SyncWindow(AppActivationSettings settings)
    {
        InitializeComponent();
        _syncService = new SyncService(settings);
    }

    private async void SyncClicked(object sender, RoutedEventArgs e)
    {
        SyncButton.IsEnabled = false;
        SyncProgressBar.Value = 0;
        LogText.Text = "Старт синхронизации...";
        CopyErrorButton.Visibility = Visibility.Collapsed;
        _lastErrorText = string.Empty;

        var progress = new Progress<SyncProgress>(UpdateProgress);

        try
        {
            var result = await _syncService.SyncAsync(progress);
            LogText.Text = result;
        }
        catch (Exception ex)
        {
            StatusTitle.Text = "Ошибка синхронизации";
            StatusTitle.Foreground = new SolidColorBrush(Color.FromRgb(249, 31, 37));
            StatusMessage.Text = ex.Message;
            LogText.Text = ex.ToString();
            _lastErrorText = $"{StatusTitle.Text}{Environment.NewLine}{Environment.NewLine}{ex}";
            CopyErrorButton.Visibility = Visibility.Visible;
        }
        finally
        {
            SyncButton.IsEnabled = true;
        }
    }

    private void UpdateProgress(SyncProgress progress)
    {
        SyncProgressBar.Value = progress.Percent;
        StatusTitle.Text = progress.Title;
        StatusMessage.Text = progress.Message;
        StatusTitle.Foreground = progress.IsError
            ? new SolidColorBrush(Color.FromRgb(249, 31, 37))
            : new SolidColorBrush(Color.FromRgb(17, 24, 39));

        LogText.Text += $"{Environment.NewLine}{DateTime.Now:HH:mm:ss}  {progress.Title}: {progress.Message}";
        if (progress.IsError)
        {
            _lastErrorText = $"{progress.Title}{Environment.NewLine}{progress.Message}";
            CopyErrorButton.Visibility = Visibility.Visible;
        }
    }

    private void CopyErrorClicked(object sender, RoutedEventArgs e)
    {
        var text = string.IsNullOrWhiteSpace(_lastErrorText) ? LogText.Text : _lastErrorText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Clipboard.SetText(text);
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
