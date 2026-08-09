using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using FFPOS.Models;

namespace FFPOS.Controls;

public partial class CategoryButton : UserControl
{
    public static readonly DependencyProperty CategoryProperty =
        DependencyProperty.Register(nameof(Category), typeof(Category), typeof(CategoryButton), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectCommandProperty =
        DependencyProperty.Register(nameof(SelectCommand), typeof(ICommand), typeof(CategoryButton), new PropertyMetadata(null));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(CategoryButton), new PropertyMetadata(false));

    public Category? Category
    {
        get => (Category?)GetValue(CategoryProperty);
        set => SetValue(CategoryProperty, value);
    }

    public ICommand? SelectCommand
    {
        get => (ICommand?)GetValue(SelectCommandProperty);
        set => SetValue(SelectCommandProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public CategoryButton()
    {
        InitializeComponent();
    }
}
