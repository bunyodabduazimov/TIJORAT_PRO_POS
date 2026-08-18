using System.Windows;
using System.Windows.Input;
using FFPOS.Models;
using FFPOS.Services;

namespace FFPOS.Views;

public partial class CustomerSelectWindow : Window
{
    private const int CustomerPageSize = 100;

    private readonly DatabaseService _databaseService = new();
    private readonly List<People> _allCustomers = new();
    private readonly List<People> _filteredCustomers = new();
    private readonly int _selectedPeopleId;
    private int _customerPage = 1;

    public CustomerSelectWindow(int selectedPeopleId = 1)
    {
        _selectedPeopleId = selectedPeopleId <= 0 ? 1 : selectedPeopleId;
        InitializeComponent();
    }

    public People? SelectedCustomer { get; private set; }

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
        LoadCustomers();
        UpdateSearchClearButton();
        SearchBox.Dispatcher.BeginInvoke(() =>
        {
            SearchBox.Focus();
            SearchBox.CaretIndex = SearchBox.Text.Length;
        });
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
        SelectCustomerById(_selectedPeopleId);
    }

    private void SearchChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        SearchBox.CaretIndex = SearchBox.Text.Length;
        UpdateSearchClearButton();
        ApplyFilter();
    }

    private void ClearSearchClicked(object sender, RoutedEventArgs e)
    {
        SearchBox.Clear();
        SearchBox.Focus();
        SearchBox.CaretIndex = SearchBox.Text.Length;
    }

    private void UpdateSearchClearButton()
    {
        ClearSearchButton.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
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

        _filteredCustomers.Clear();
        _filteredCustomers.AddRange(items);
        _customerPage = 1;
        ApplyPage();
    }

    private void ApplyPage()
    {
        var totalPages = GetCustomerTotalPages();
        _customerPage = Math.Clamp(_customerPage, 1, totalPages);

        var pageItems = _filteredCustomers
            .Skip((_customerPage - 1) * CustomerPageSize)
            .Take(CustomerPageSize)
            .ToList();

        CustomersList.ItemsSource = null;
        CustomersList.ItemsSource = pageItems;

        CustomerPageText.Text = $"{_customerPage} / {totalPages} • {_filteredCustomers.Count}";
        CustomerPager.Visibility = _filteredCustomers.Count > CustomerPageSize ? Visibility.Visible : Visibility.Collapsed;
        PreviousPageButton.IsEnabled = _customerPage > 1;
        NextPageButton.IsEnabled = _customerPage < totalPages;
    }

    private int GetCustomerTotalPages()
    {
        return Math.Max(1, (int)Math.Ceiling(_filteredCustomers.Count / (double)CustomerPageSize));
    }

    private void SelectCustomerById(int customerId)
    {
        var index = _filteredCustomers.FindIndex(customer => customer.Id == customerId);
        if (index >= 0)
        {
            _customerPage = index / CustomerPageSize + 1;
            ApplyPage();
            CustomersList.SelectedItem = _filteredCustomers[index];
            return;
        }

        CustomersList.SelectedItem = _filteredCustomers.FirstOrDefault();
    }

    private async void AddCustomerClicked(object sender, RoutedEventArgs e)
    {
        var window = new AddCustomerWindow
        {
            Owner = this
        };

        if (window.ShowDialog() != true || window.Customer is null)
        {
            return;
        }

        try
        {
            ErrorText.Text = "Создание контрагента на сервере...";
            var createdOnServer = await new SyncService(new AppSettingsService().Load()).CreatePeopleAsync(window.Customer);
            var customer = await _databaseService.AddPeopleAsync(createdOnServer);

            var existingIndex = _allCustomers.FindIndex(item => item.Id == customer.Id);
            if (existingIndex >= 0)
            {
                _allCustomers[existingIndex] = customer;
            }
            else
            {
                _allCustomers.Add(customer);
            }

            ErrorText.Text = string.Empty;
            ApplyFilter();
            SelectCustomerById(customer.Id);
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Не удалось создать контрагента на сервере. {ex.Message}";
        }
    }

    private void PreviousPageClicked(object sender, RoutedEventArgs e)
    {
        _customerPage--;
        ApplyPage();
    }

    private void NextPageClicked(object sender, RoutedEventArgs e)
    {
        _customerPage++;
        ApplyPage();
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
