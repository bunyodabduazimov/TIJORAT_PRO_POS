using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace FFPOS.Controls;

public partial class SettingTextField : UserControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingTextField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text), typeof(string), typeof(SettingTextField),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IconKindProperty = DependencyProperty.Register(
        nameof(IconKind), typeof(PackIconKind), typeof(SettingTextField), new PropertyMetadata(PackIconKind.CogOutline));

    public static readonly DependencyProperty IsNumericProperty = DependencyProperty.Register(
        nameof(IsNumeric), typeof(bool), typeof(SettingTextField), new PropertyMetadata(false));

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(SettingTextField), new PropertyMetadata(false));

    public static readonly DependencyProperty FloatingHeaderProperty = DependencyProperty.Register(
        nameof(FloatingHeader), typeof(bool), typeof(SettingTextField), new PropertyMetadata(false, OnFloatingHeaderChanged));

    public static readonly DependencyProperty FieldHeightProperty = DependencyProperty.Register(
        nameof(FieldHeight), typeof(double), typeof(SettingTextField), new PropertyMetadata(42d, OnFieldHeightChanged));

    public SettingTextField()
    {
        InitializeComponent();
        UpdateHeaderMode();
        UpdateFieldHeight();
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public PackIconKind IconKind
    {
        get => (PackIconKind)GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    public bool IsNumeric
    {
        get => (bool)GetValue(IsNumericProperty);
        set => SetValue(IsNumericProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public bool FloatingHeader
    {
        get => (bool)GetValue(FloatingHeaderProperty);
        set => SetValue(FloatingHeaderProperty, value);
    }

    public double FieldHeight
    {
        get => (double)GetValue(FieldHeightProperty);
        set => SetValue(FieldHeightProperty, value);
    }

    private static void OnFloatingHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SettingTextField)d).UpdateHeaderMode();
    }

    private void UpdateHeaderMode()
    {
        HeaderText.Visibility = FloatingHeader ? Visibility.Collapsed : Visibility.Visible;
        FloatingHeaderText.Visibility = FloatingHeader ? Visibility.Visible : Visibility.Collapsed;
        FieldBorder.Margin = FloatingHeader ? new Thickness(0, 8, 0, 0) : new Thickness(0);
    }

    private static void OnFieldHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SettingTextField)d).UpdateFieldHeight();
    }

    private void UpdateFieldHeight()
    {
        FieldRow.Height = new GridLength(FieldHeight);
        InputBox.Height = FieldHeight;
    }

    private void InputBoxPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!IsNumeric)
        {
            return;
        }

        e.Handled = e.Text.Any(ch => !char.IsDigit(ch));
    }
}
