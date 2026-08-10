using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using FFPOS.Models;
using FFPOS.Services;
using FFPOS.Views;

namespace FFPOS.ViewModels;

public class SalesViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _databaseService = new();
    private readonly List<Product> _allProducts = new();
    private Category? _selectedCategory;
    private Order? _currentOrder;
    private string _searchText = string.Empty;
    private string _selectedOrderType = "В зале";
    private string _activeSection = "Касса";
    private string _selectedPaymentType = "Наличные";
    private bool _isSidebarOpen = true;
    private bool _isTableView;
    private int _nextOrderNumber = 106;
    private string _currentTime = DateTime.Now.ToString("HH:mm");
    private string _currentDate = DateTime.Now.ToString("dd.MM.yyyy");
    private bool _isLoading;

    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Order> OpenOrders { get; } = new();

    public Order? CurrentOrder
    {
        get => _currentOrder;
        set
        {
            if (_currentOrder == value)
            {
                return;
            }

            _currentOrder = value;
            SelectedOrderType = value?.OrderType ?? "В зале";
            foreach (var order in OpenOrders)
            {
                order.IsSelected = order == value;
            }

            RefreshProductQuantities();
            OnPropertyChanged();
        }
    }

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (_selectedCategory == value)
            {
                return;
            }

            _selectedCategory = value;
            foreach (var category in Categories)
            {
                category.IsSelected = category == value;
            }

            OnPropertyChanged();
            FilterProducts();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value)
            {
                return;
            }

            _searchText = value;
            OnPropertyChanged();
            FilterProducts();
        }
    }

    public string SelectedOrderType
    {
        get => _selectedOrderType;
        set
        {
            if (_selectedOrderType == value)
            {
                return;
            }

            _selectedOrderType = value;
            if (CurrentOrder is not null)
            {
                CurrentOrder.OrderType = value;
                if (!_isLoading)
                {
                    SaveCurrentOrder();
                }
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(IsDineInSelected));
            OnPropertyChanged(nameof(IsTakeAwaySelected));
            OnPropertyChanged(nameof(IsDeliverySelected));
        }
    }

    public string ActiveSection
    {
        get => _activeSection;
        set
        {
            if (_activeSection == value)
            {
                return;
            }

            _activeSection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCashierSection));
            OnPropertyChanged(nameof(IsOrdersSection));
            OnPropertyChanged(nameof(IsKitchenSection));
            OnPropertyChanged(nameof(IsReportsSection));
        }
    }

    public string SelectedPaymentType
    {
        get => _selectedPaymentType;
        set
        {
            if (_selectedPaymentType == value)
            {
                return;
            }

            _selectedPaymentType = value;
            OnPropertyChanged();
        }
    }

    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set
        {
            if (_isSidebarOpen == value)
            {
                return;
            }

            _isSidebarOpen = value;
            OnPropertyChanged();
        }
    }

    public bool IsTableView
    {
        get => _isTableView;
        set
        {
            if (_isTableView == value)
            {
                return;
            }

            _isTableView = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCardView));
            OnPropertyChanged(nameof(ProductViewIcon));
        }
    }

    public bool IsCardView => !IsTableView;
    public bool IsDineInSelected => SelectedOrderType == "В зале";
    public bool IsTakeAwaySelected => SelectedOrderType == "С собой";
    public bool IsDeliverySelected => SelectedOrderType == "Доставка";
    public bool IsCashierSection => ActiveSection == "Касса";
    public bool IsOrdersSection => ActiveSection == "Заказы";
    public bool IsKitchenSection => ActiveSection == "Кухня";
    public bool IsReportsSection => ActiveSection == "Отчёты";
    public string ProductViewIcon => IsTableView ? "ViewGridOutline" : "FormatListBulleted";

    public string CurrentTime
    {
        get => _currentTime;
        private set
        {
            _currentTime = value;
            OnPropertyChanged();
        }
    }

    public string CurrentDate
    {
        get => _currentDate;
        private set
        {
            _currentDate = value;
            OnPropertyChanged();
        }
    }

    public ICommand AddProductCommand { get; }
    public ICommand SelectCategoryCommand { get; }
    public ICommand CreateNewOrderCommand { get; }
    public ICommand SelectOrderCommand { get; }
    public ICommand IncreaseQuantityCommand { get; }
    public ICommand DecreaseQuantityCommand { get; }
    public ICommand RemoveOrderItemCommand { get; }
    public ICommand ClearOrderCommand { get; }
    public ICommand HoldOrderCommand { get; }
    public ICommand PayCommand { get; }
    public ICommand SelectOrderTypeCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand OpenProfileCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand SelectPaymentTypeCommand { get; }
    public ICommand ToggleProductViewCommand { get; }
    public ICommand AddNoteCommand { get; }
    public ICommand EditDiscountCommand { get; }
    public ICommand ShowCustomerCommand { get; }
    public ICommand ShowReceiptCommand { get; }
    public ICommand ShowOrderActionsCommand { get; }

    public SalesViewModel()
    {
        AddProductCommand = new RelayCommand<Product>(AddProduct);
        SelectCategoryCommand = new RelayCommand<Category>(category => SelectedCategory = category);
        CreateNewOrderCommand = new RelayCommand(CreateNewOrder);
        SelectOrderCommand = new RelayCommand<Order>(order => CurrentOrder = order);
        IncreaseQuantityCommand = new RelayCommand<OrderItem>(IncreaseQuantity);
        DecreaseQuantityCommand = new RelayCommand<OrderItem>(DecreaseQuantity);
        RemoveOrderItemCommand = new RelayCommand<OrderItem>(RemoveOrderItem);
        ClearOrderCommand = new RelayCommand(ClearOrder);
        HoldOrderCommand = new RelayCommand(HoldOrder);
        PayCommand = new RelayCommand(Pay);
        SelectOrderTypeCommand = new RelayCommand<string>(type => SelectedOrderType = type ?? "В зале");
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarOpen = !IsSidebarOpen);
        NavigateCommand = new RelayCommand<string>(Navigate);
        OpenProfileCommand = new RelayCommand(OpenProfile);
        OpenSettingsCommand = new RelayCommand(OpenProfile);
        LogoutCommand = new RelayCommand(() => AppDialogWindow.ShowInfo("Выход из смены выполнен."));
        SelectPaymentTypeCommand = new RelayCommand<string>(type => SelectedPaymentType = type ?? "Наличные");
        ToggleProductViewCommand = new RelayCommand(() => IsTableView = !IsTableView);
        AddNoteCommand = new RelayCommand(() => AppDialogWindow.ShowInfo("Примечание добавлено к текущему чеку."));
        EditDiscountCommand = new RelayCommand(EditDiscount);
        ShowCustomerCommand = new RelayCommand(() => AppDialogWindow.ShowInfo("Карточка клиента для текущего чека."));
        ShowReceiptCommand = new RelayCommand(() => AppDialogWindow.ShowInfo("Печать/просмотр пречека."));
        ShowOrderActionsCommand = new RelayCommand(() => AppDialogWindow.ShowInfo("Дополнительные действия по чеку."));

        LoadInitialData();
        FilterProducts();
        StartClock();
    }

    private void LoadInitialData()
    {
        _isLoading = true;
        try
        {
            _databaseService.InitializeAsync().GetAwaiter().GetResult();

            Categories.Clear();
            Categories.Add(new Category
            {
                Id = 0,
                Name = "\u0412\u0441\u0435 \u0442\u043e\u0432\u0430\u0440\u044b",
                IconPath = "/Assets/Images/default.png",
                IconGlyph = "\uE8EF"
            });
            foreach (var category in _databaseService.GetCategoriesAsync().GetAwaiter().GetResult())
            {
                Categories.Add(category);
            }

            _allProducts.Clear();
            _allProducts.AddRange(_databaseService.GetProductsAsync().GetAwaiter().GetResult());

            OpenOrders.Clear();
            var openOrders = _databaseService.GetOpenOrdersAsync(_allProducts).GetAwaiter().GetResult().ToList();
            CurrentOrder = openOrders.OrderBy(order => order.Number).LastOrDefault();
            foreach (var order in openOrders.Where(order => order != CurrentOrder))
            {
                OpenOrders.Add(order);
            }

            _nextOrderNumber = _databaseService.GetNextOrderNumberAsync().GetAwaiter().GetResult();
            if (CurrentOrder is null)
            {
                CurrentOrder = CreateOrder(saveToDatabase: false);
            }

            SelectedCategory = Categories.FirstOrDefault(category => category.Id == 0) ?? Categories.FirstOrDefault();
        }
        finally
        {
            _isLoading = false;
        }

        RefreshProductQuantities();
    }

    private void Navigate(string? section)
    {
        if (string.IsNullOrWhiteSpace(section))
        {
            return;
        }

        ActiveSection = section;
        if (section != "Касса")
        {
            AppDialogWindow.ShowInfo($"Раздел \"{section}\" открыт.");
        }
    }

    private void OpenProfile()
    {
        var settingsService = new AppSettingsService();
        var previousAppType = settingsService.Load().AppType;
        var window = new SettingsWindow
        {
            Owner = Application.Current?.MainWindow
        };
        window.ShowDialog();

        if (!window.WasSaved)
        {
            return;
        }

        var settings = settingsService.Load();
        if (settings.AppType != previousAppType)
        {
            App.SwitchMainWindow(settings);
        }
    }

    private void EditDiscount()
    {
        if (CurrentOrder is null)
        {
            return;
        }

        var window = new DiscountWindow(CurrentOrder.Subtotal, CurrentOrder.Discount)
        {
            Owner = Application.Current?.MainWindow
        };

        if (window.ShowDialog() == true)
        {
            CurrentOrder.Discount = window.Discount;
            CurrentOrder.RefreshTotals();
            SaveCurrentOrder();
        }
    }

    private void AddProduct(Product? product)
    {
        if (product is null)
        {
            return;
        }

        CurrentOrder ??= CreateOrder();
        var existing = CurrentOrder.Items.FirstOrDefault(item => item.Product.Id == product.Id);
        if (existing is not null)
        {
            existing.Quantity++;
        }
        else
        {
            CurrentOrder.Items.Add(new OrderItem
            {
                Product = product,
                ProductId = product.Id,
                Quantity = 1,
                Price = product.Price
            });
        }

        CurrentOrder.RefreshTotals();
        RefreshProductQuantities();
        SaveCurrentOrder();
    }

    private void IncreaseQuantity(OrderItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.Quantity++;
        CurrentOrder?.RefreshTotals();
        RefreshProductQuantities();
        SaveCurrentOrder();
    }

    private void DecreaseQuantity(OrderItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.Quantity <= 1)
        {
            RemoveOrderItem(item);
            return;
        }

        item.Quantity--;
        CurrentOrder?.RefreshTotals();
        RefreshProductQuantities();
    }

    private void RemoveOrderItem(OrderItem? item)
    {
        if (item is null || CurrentOrder is null)
        {
            return;
        }

        CurrentOrder.Items.Remove(item);
        CurrentOrder.RefreshTotals();
        RefreshProductQuantities();
        SaveCurrentOrder();
    }

    private void ClearOrder()
    {
        if (CurrentOrder is null || CurrentOrder.Items.Count == 0)
        {
            return;
        }

        if (AppDialogWindow.Confirm("Очистить текущий чек?"))
        {
            CurrentOrder.Items.Clear();
            CurrentOrder.RefreshTotals();
            RefreshProductQuantities();
            SaveCurrentOrder();
        }
    }

    private void HoldOrder()
    {
        if (CurrentOrder is null)
        {
            return;
        }

        if (!OpenOrders.Contains(CurrentOrder))
        {
            OpenOrders.Add(CurrentOrder);
        }

        SaveCurrentOrder();
        AppDialogWindow.ShowInfo($"{CurrentOrder.DisplayName} отложен.");
    }

    private void Pay()
    {
        if (CurrentOrder is null || CurrentOrder.Items.Count == 0)
        {
            return;
        }

        var window = new PaymentWindow(CurrentOrder.Total, SelectedPaymentType)
        {
            Owner = Application.Current?.MainWindow
        };

        var paymentResult = window.ShowDialog();

        if (window.HoldRequested)
        {
            HoldOrder();
            return;
        }

        if (paymentResult == true)
        {
            SelectedPaymentType = window.PaymentType;
            _databaseService.MarkOrderPaidAsync(CurrentOrder).GetAwaiter().GetResult();
            OpenOrders.Remove(CurrentOrder);
            CurrentOrder = OpenOrders.FirstOrDefault() ?? CreateOrder();
            RefreshProductQuantities();
        }
    }

    private void CreateNewOrder()
    {
        CurrentOrder = CreateOrder();
        RefreshProductQuantities();
    }

    private Order CreateOrder(bool saveToDatabase = true)
    {
        var order = new Order { Number = _nextOrderNumber++, OrderType = SelectedOrderType };
        OpenOrders.Add(order);
        OnPropertyChanged(nameof(OpenOrders));
        if (saveToDatabase && !_isLoading)
        {
            SaveCurrentOrder(order);
        }

        return order;
    }

    private void SaveCurrentOrder()
    {
        if (CurrentOrder is not null)
        {
            SaveCurrentOrder(CurrentOrder);
        }
    }

    private void SaveCurrentOrder(Order order)
    {
        if (_isLoading)
        {
            return;
        }

        try
        {
            _databaseService.SaveOpenOrderAsync(order).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            AppDialogWindow.ShowError($"Не удалось сохранить чек в базе данных.\n{ex.Message}");
        }
    }

    private void FilterProducts()
    {
        Products.Clear();
        var query = SearchText.Trim();
        var filtered = _allProducts.Where(product =>
            (SelectedCategory is null || SelectedCategory.Id == 0 || product.CategoryId == SelectedCategory.Id) &&
            (string.IsNullOrWhiteSpace(query) || product.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)));

        foreach (var product in filtered)
        {
            Products.Add(product);
        }
    }

    private void RefreshProductQuantities()
    {
        foreach (var product in _allProducts)
        {
            product.SelectedQuantity = CurrentOrder?.Items
                .Where(item => item.Product.Id == product.Id)
                .Sum(item => item.Quantity) ?? 0;
        }
    }

    private void StartClock()
    {
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        timer.Tick += (_, _) =>
        {
            CurrentTime = DateTime.Now.ToString("HH:mm");
            CurrentDate = DateTime.Now.ToString("dd.MM.yyyy");
        };
        timer.Start();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Predicate<T?>? _canExecute;

    public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}
