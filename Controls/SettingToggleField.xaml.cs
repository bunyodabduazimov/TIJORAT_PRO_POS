using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace FFPOS.Controls;

public partial class SettingToggleField : UserControl
{
    public event EventHandler? Toggled;

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingToggleField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked), typeof(bool), typeof(SettingToggleField),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsCheckedChanged));

    public SettingToggleField()
    {
        InitializeComponent();
        UpdateVisual();
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (SettingToggleField)d;
        control.UpdateVisual();
        control.Toggled?.Invoke(control, EventArgs.Empty);
    }

    private void StateButton_Click(object sender, RoutedEventArgs e)
    {
        IsChecked = !IsChecked;
    }

    private void UpdateVisual()
    {
        StateButton.Background = IsChecked ? new SolidColorBrush(Color.FromRgb(22, 163, 74)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
        StateButton.BorderBrush = IsChecked ? new SolidColorBrush(Color.FromRgb(22, 163, 74)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));
        StateText.Text = IsChecked ? "Вкл" : "Выкл";
        StateText.Foreground = IsChecked ? Brushes.White : new SolidColorBrush(Color.FromRgb(51, 65, 85));
        StateIcon.Kind = IsChecked ? PackIconKind.Check : PackIconKind.Close;
        StateIcon.Foreground = IsChecked ? Brushes.White : new SolidColorBrush(Color.FromRgb(71, 85, 105));
    }
}
