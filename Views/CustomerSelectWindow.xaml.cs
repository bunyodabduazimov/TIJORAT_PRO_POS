using System.Windows;
using System.Windows.Input;
using FFPOS.Models;
using FFPOS.Services;

namespace FFPOS.Views;

public partial class CustomerSelectWindow : Window
{
    private readonly DatabaseService _databaseService = new();
    private readonly List<People> _allCustomers = new();
    private readonly int _selectedPeopleId;

    public CustomerSelectWindow(int selectedPeopleId = 1)
    {
        _selectedPeopleId = selectedPeopleId <= 0 ? 1 : selectedPeopleId;
        InitializeComponent();
    }

    public People? SelectedCustomer { get; private set; }

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        LoadCustomers();
    }

    private void LoadCustomers()
    {
        _databaseService.InitializeAsync().GetAwaiter().GetResult();
        _allCustomers.Clear();
        _allCustomers.AddRange(_databaseService.GetPeoplesAsync().GetAwaiter().GetResult());

        if (_allCustomers.All(customer => customer.Id != 1))
        {
            _allCustomers.Insert(0, new People
            {
                Id = 1,
                Name = "Розничный покупатель",
                Phone = string.Empty,
                Address = string.Empty,
                Balance = 0,
                Status = 1
            });
        }

        ApplyFilter();
        CustomersList.SelectedItem = _allCustomers.FirstOrDefault(customer => customer.Id == _selectedPeopleId)
            ?? _allCustomers.FirstOrDefault();
    }

    private void SearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text.Trim();
        var items = string.IsNullOrWhiteSpace(query)
            ? _allCustomers
            : _allCustomers
                .Where(customer =>
                    Contains(customer.Name, query) ||
                    Contains(customer.Phone, query) ||
                    Contains(customer.Address, query))
                .ToList();

        CustomersList.ItemsSource = null;
        CustomersList.ItemsSource = items;
    }

    private void AddCustomerClicked(object sender, RoutedEventArgs e)
    {
        var window = new AddCustomerWindow
        {
            Owner = this
        };

        if (window.ShowDialog() != true || window.Customer is null)
        {
            return;
        }

        var customer = _databaseService.AddPeopleAsync(window.Customer).GetAwaiter().GetResult();

        _allCustomers.Add(customer);
        ErrorText.Text = string.Empty;
        ApplyFilter();
        CustomersList.SelectedItem = customer;
    }

    private void CustomerDoubleClicked(object sender, MouseButtonEventArgs e)
    {
        SelectCurrent();
    }

    private void SelectClicked(object sender, RoutedEventArgs e)
    {
        SelectCurrent();
    }

    private void SelectCurrent()
    {
        if (CustomersList.SelectedItem is not People customer)
        {
            ErrorText.Text = "Выберите клиента";
            return;
        }

        SelectedCustomer = customer;
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

    private static bool Contains(string? value, string query)
    {
        return value?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;
    }
}
