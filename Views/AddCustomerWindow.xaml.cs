using System.Windows;
using System.Windows.Input;
using FFPOS.Models;

namespace FFPOS.Views;

public partial class AddCustomerWindow : Window
{
    public AddCustomerWindow()
    {
        InitializeComponent();
    }

    public People? Customer { get; private set; }

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        NameBox.Focus();
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "Введите название клиента";
            NameBox.Focus();
            return;
        }

        Customer = new People
        {
            Name = NameBox.Text.Trim(),
            Phone = PhoneBox.Text.Trim(),
            Address = AddressBox.Text.Trim(),
            Balance = 0,
            Status = 1
        };

        DialogResult = true;
        Close();
    }

    private void CancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
