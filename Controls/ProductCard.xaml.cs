using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FFPOS.Models;

namespace FFPOS.Controls;

public partial class ProductCard : UserControl
{
    public static readonly DependencyProperty ProductProperty =
        DependencyProperty.Register(nameof(Product), typeof(Product), typeof(ProductCard), new PropertyMetadata(null));

    public static readonly DependencyProperty AddCommandProperty =
        DependencyProperty.Register(nameof(AddCommand), typeof(ICommand), typeof(ProductCard), new PropertyMetadata(null));

    public Product? Product
    {
        get => (Product?)GetValue(ProductProperty);
        set => SetValue(ProductProperty, value);
    }

    public ICommand? AddCommand
    {
        get => (ICommand?)GetValue(AddCommandProperty);
        set => SetValue(AddCommandProperty, value);
    }

    public ProductCard()
    {
        InitializeComponent();
    }

    private void ProductImage_OnImageFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        if (sender is not Image image)
        {
            return;
        }

        image.ImageFailed -= ProductImage_OnImageFailed;
        image.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/Images/default.png", UriKind.Absolute));
        image.ImageFailed += ProductImage_OnImageFailed;
    }
}
