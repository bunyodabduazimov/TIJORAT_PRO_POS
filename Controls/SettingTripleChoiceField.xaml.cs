using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FFPOS.Controls;

public partial class SettingTripleChoiceField : UserControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingTripleChoiceField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty FirstTextProperty = DependencyProperty.Register(
        nameof(FirstText), typeof(string), typeof(SettingTripleChoiceField), new PropertyMetadata("Общий"));

    public static readonly DependencyProperty SecondTextProperty = DependencyProperty.Register(
        nameof(SecondText), typeof(string), typeof(SettingTripleChoiceField), new PropertyMetadata("Аптека"));

    public static readonly DependencyProperty ThirdTextProperty = DependencyProperty.Register(
        nameof(ThirdText), typeof(string), typeof(SettingTripleChoiceField), new PropertyMetadata("Фаст Фуд"));

    public static readonly DependencyProperty SelectedValueProperty = DependencyProperty.Register(
        nameof(SelectedValue), typeof(int), typeof(SettingTripleChoiceField),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedValueChanged));

    public SettingTripleChoiceField()
    {
        InitializeComponent();
        UpdateVisual();
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string FirstText
    {
        get => (string)GetValue(FirstTextProperty);
        set => SetValue(FirstTextProperty, value);
    }

    public string SecondText
    {
        get => (string)GetValue(SecondTextProperty);
        set => SetValue(SecondTextProperty, value);
    }

    public string ThirdText
    {
        get => (string)GetValue(ThirdTextProperty);
        set => SetValue(ThirdTextProperty, value);
    }

    public int SelectedValue
    {
        get => (int)GetValue(SelectedValueProperty);
        set => SetValue(SelectedValueProperty, value);
    }

    private static void OnSelectedValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SettingTripleChoiceField)d).UpdateVisual();
    }

    private void FirstClicked(object sender, RoutedEventArgs e) => SelectedValue = 1;

    private void SecondClicked(object sender, RoutedEventArgs e) => SelectedValue = 2;

    private void ThirdClicked(object sender, RoutedEventArgs e) => SelectedValue = 3;

    private void UpdateVisual()
    {
        if (SelectedValue is < 1 or > 3)
        {
            SelectedValue = 1;
            return;
        }

        ApplyState(FirstButton, FirstIcon, FirstLabel, SelectedValue == 1);
        ApplyState(SecondButton, SecondIcon, SecondLabel, SelectedValue == 2);
        ApplyState(ThirdButton, ThirdIcon, ThirdLabel, SelectedValue == 3);
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
