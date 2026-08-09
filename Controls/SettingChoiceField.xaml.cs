using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FFPOS.Controls;

public partial class SettingChoiceField : UserControl
{
    public event EventHandler? SelectionChanged;

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingChoiceField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LeftTextProperty = DependencyProperty.Register(
        nameof(LeftText), typeof(string), typeof(SettingChoiceField), new PropertyMetadata("Количество"));

    public static readonly DependencyProperty RightTextProperty = DependencyProperty.Register(
        nameof(RightText), typeof(string), typeof(SettingChoiceField), new PropertyMetadata("Упаковка"));

    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(
        nameof(SelectedValue), typeof(int), typeof(SettingChoiceField),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedValueChanged));

    public SettingChoiceField()
    {
        InitializeComponent();
        UpdateVisual();
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string LeftText
    {
        get => (string)GetValue(LeftTextProperty);
        set => SetValue(LeftTextProperty, value);
    }

    public string RightText
    {
        get => (string)GetValue(RightTextProperty);
        set => SetValue(RightTextProperty, value);
    }

    public int SelectedValue
    {
        get => (int)GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SettingChoiceField)d).UpdateVisual();
    }

    private void LeftClicked(object sender, RoutedEventArgs e)
    {
        SelectedValue = 1;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RightClicked(object sender, RoutedEventArgs e)
    {
        SelectedValue = 2;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateVisual()
    {
        var leftActive = SelectedValue != 2;
        ApplyState(LeftButton, LeftIcon, LeftLabel, leftActive);
        ApplyState(RightButton, RightIcon, RightLabel, !leftActive);
    }

    private static void ApplyState(Button button, FrameworkElement icon, TextBlock label, bool active)
    {
        button.Background = active
            ? new SolidColorBrush(Color.FromRgb(249, 31, 37))
            : Brushes.Transparent;

        var foreground = active
            ? Brushes.White
            : new SolidColorBrush(Color.FromRgb(52, 64, 84));

        button.Foreground = foreground;
        label.Foreground = foreground;

        if (icon is Control control)
        {
            control.Foreground = foreground;
        }
    }
}
