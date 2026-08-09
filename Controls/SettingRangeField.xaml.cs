using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MaterialDesignThemes.Wpf;

namespace FFPOS.Controls;

public partial class SettingRangeField : UserControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingRangeField), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(int), typeof(SettingRangeField), new PropertyMetadata(0, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(int), typeof(SettingRangeField), new PropertyMetadata(100, OnRangeChanged));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(SettingRangeField),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnRangeChanged));

    public static readonly DependencyProperty SuffixProperty = DependencyProperty.Register(
        nameof(Suffix), typeof(string), typeof(SettingRangeField), new PropertyMetadata(string.Empty, OnRangeChanged));

    public static readonly DependencyProperty IconKindProperty = DependencyProperty.Register(
        nameof(IconKind), typeof(PackIconKind), typeof(SettingRangeField), new PropertyMetadata(PackIconKind.Tune));

    private bool _isDragging;

    public SettingRangeField()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateVisual();
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public int Minimum
    {
        get => (int)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Suffix
    {
        get => (string)GetValue(SuffixProperty);
        set => SetValue(SuffixProperty, value);
    }

    public PackIconKind IconKind
    {
        get => (PackIconKind)GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((SettingRangeField)d).CoerceAndUpdate();
    }

    private void CoerceAndUpdate()
    {
        if (Maximum < Minimum)
        {
            Maximum = Minimum;
            return;
        }

        var clamped = Math.Clamp(Value, Minimum, Maximum);
        if (clamped != Value)
        {
            Value = clamped;
            return;
        }

        UpdateVisual();
    }

    private void TrackMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        TrackHost.CaptureMouse();
        SetValueFromPoint(e.GetPosition(TrackHost).X);
    }

    private void TrackMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            SetValueFromPoint(e.GetPosition(TrackHost).X);
        }
    }

    private void TrackMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        TrackHost.ReleaseMouseCapture();
    }

    private void TrackHostSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateVisual();
    }

    private void SetValueFromPoint(double x)
    {
        var width = Math.Max(1, TrackHost.ActualWidth);
        var ratio = Math.Clamp(x / width, 0, 1);
        Value = Minimum + (int)Math.Round((Maximum - Minimum) * ratio);
    }

    private void UpdateVisual()
    {
        if (!IsLoaded)
        {
            return;
        }

        var range = Math.Max(1, Maximum - Minimum);
        var ratio = Math.Clamp((double)(Value - Minimum) / range, 0, 1);
        var width = Math.Max(0, TrackHost.ActualWidth);
        var fillWidth = width * ratio;

        FillBar.Width = fillWidth;
        Thumb.Margin = new Thickness(Math.Max(0, fillWidth - Thumb.Width / 2), 0, 0, 0);
        ValueText.Text = string.IsNullOrWhiteSpace(Suffix) ? Value.ToString() : $"{Value} {Suffix}";
    }
}
