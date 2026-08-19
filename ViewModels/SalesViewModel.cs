using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using FFPOS.Models;
using FFPOS.Services;
using FFPOS.Views;

namespace FFPOS.ViewModels;

public class SalesViewModel : INotifyPropertyChanged
{
    private const int CardProductPageSize = 200;
    private const int TableProductPageSize = 100;
    private const int OrderPageSize = 100;
    private const int PaymentPageSize = 100;

    private readonly DatabaseService _databaseService = new();
    private readonly PrintService _printService = new();
    private readonly List<Product> _allProducts = new();
    private Category? _selectedCategory;
    private Order? _currentOrder;
    private string _searchText = string.Empty;
    private string _selectedOrderType = "В зале";
    private string _activeSection = "Касса";
    private string _selectedPaymentType = "Наличные";
    private bool _isSidebarOpen = true;
    private bool _isTableView;
    private int _productPage = 1;
    private int _productTotalCount;
    private int _nextOrderNumber = 106;
    private string _currentTime = DateTime.Now.ToString("HH:mm");
    private string _currentDate = DateTime.Now.ToString("dd.MM.yyyy");
    private string _orderSearchText = string.Empty;
    private string _paymentSearchText = string.Empty;
    private string _orderFilter = "open";
    private int _orderPage = 1;
    private int _orderTotalCount;
    private int _paymentPage = 1;
    private int _paymentTotalCount;
    private decimal _paymentIncomeTotal;
    private decimal _paymentExpenseTotal;
    private OrderListItem? _selectedOrderListItem;
    private PaymentListItem? _selectedPaymentListItem;
    private bool _isLoading;

    public ObservableCollection<Category> Categories { get; } = new();
    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Order> OpenOrders { get; } = new();
    public ObservableCollection<OrderListItem> Orders { get; } = new();
    public ObservableCollection<PaymentListItem> Payments { get; } = new();
    private List<OrderListItem> _allOrderItems = new();
    private List<PaymentListItem> _allPayments = new();

    public event EventHandler<OrderItem>? OrderItemTouched;

    public string CashierName
    {
        get
        {
            var user = App.CurrentUser;
            return string.IsNullOrWhiteSpace(user?.Name)
                ? (string.IsNullOrWhiteSpace(user?.Username) ? "Кассир" : user.Username!)
                : user.Name!;
        }
    }

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
            _productPage = 1;
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
            _productPage = 1;
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
            OnPropertyChanged(nameof(IsPaymentsSection));
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
            _productPage = 1;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCardView));
            OnPropertyChanged(nameof(ProductViewIcon));
            FilterProducts();
        }
    }

    public bool IsCardView => !IsTableView;
    public bool IsDineInSelected => SelectedOrderType == "В зале";
    public bool IsTakeAwaySelected => SelectedOrderType == "С собой";
    public bool IsDeliverySelected => SelectedOrderType == "Доставка";
    public bool IsCashierSection => ActiveSection == "Касса";
    public bool IsOrdersSection => ActiveSection == "Заказы";
    public bool IsPaymentsSection => ActiveSection == "Платежи";
    public bool IsKitchenSection => ActiveSection == "Кухня";
    public bool IsReportsSection => ActiveSection == "Отчёты";
    public string ProductViewIcon => IsTableView ? "ViewGridOutline" : "FormatListBulleted";
    public int ProductPage => _productPage;
    public int ProductPageSize => IsTableView ? TableProductPageSize : CardProductPageSize;
    public int ProductTotalPages => Math.Max(1, (int)Math.Ceiling(_productTotalCount / (double)ProductPageSize));
    public string ProductPageText => $"{ProductPage} / {ProductTotalPages} • {_productTotalCount}";
    public bool IsProductPagerVisible => _productTotalCount > ProductPageSize;
    public bool IsOpenOrdersFilter => OrderFilter == "open";
    public bool IsPaidOrdersFilter => OrderFilter == "paid";
    public bool IsTodayOrdersFilter => OrderFilter == "today";
    public bool IsAllOrdersFilter => OrderFilter == "all";
    public bool HasSelectedOrder => SelectedOrderListItem is not null;
    public string OrdersSummary => $"{_orderTotalCount} заказов";
    public int OrderPage => _orderPage;
    public int OrderTotalPages => Math.Max(1, (int)Math.Ceiling(_orderTotalCount / (double)OrderPageSize));
    public string OrderPageText => $"{OrderPage} / {OrderTotalPages} • {_orderTotalCount}";
    public bool IsOrderPagerVisible => _orderTotalCount > OrderPageSize;
    public bool HasSelectedPayment => SelectedPaymentListItem is not null;
    public string PaymentsSummary => $"{_paymentTotalCount} операций";
    public int PaymentPage => _paymentPage;
    public int PaymentTotalPages => Math.Max(1, (int)Math.Ceiling(_paymentTotalCount / (double)PaymentPageSize));
    public string PaymentPageText => $"{PaymentPage} / {PaymentTotalPages} • {_paymentTotalCount}";
    public bool IsPaymentPagerVisible => _paymentTotalCount > PaymentPageSize;
    public string PaymentIncomeTotalText => $"+{_paymentIncomeTotal:0.00} c";
    public string PaymentExpenseTotalText => $"-{_paymentExpenseTotal:0.00} c";

    public string OrderSearchText
    {
        get => _orderSearchText;
        set
        {
            if (_orderSearchText == value)
            {
                return;
            }

            _orderSearchText = value;
            _orderPage = 1;
            OnPropertyChanged();
            RefreshOrders();
        }
    }

    public string PaymentSearchText
    {
        get => _paymentSearchText;
        set
        {
            if (_paymentSearchText == value)
            {
                return;
            }

            _paymentSearchText = value;
            _paymentPage = 1;
            OnPropertyChanged();
            ApplyPaymentFilter();
        }
    }

    public string OrderFilter
    {
        get => _orderFilter;
        set
        {
            if (_orderFilter == value)
            {
                return;
            }

            _orderFilter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOpenOrdersFilter));
            OnPropertyChanged(nameof(IsPaidOrdersFilter));
            OnPropertyChanged(nameof(IsTodayOrdersFilter));
            OnPropertyChanged(nameof(IsAllOrdersFilter));
            _orderPage = 1;
            RefreshOrders();
        }
    }

    public OrderListItem? SelectedOrderListItem
    {
        get => _selectedOrderListItem;
        set
        {
            if (_selectedOrderListItem == value)
            {
                return;
            }

            _selectedOrderListItem = value;
            foreach (var order in Orders)
            {
                order.IsSelected = order == value;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedOrder));
        }
    }

    public PaymentListItem? SelectedPaymentListItem
    {
        get => _selectedPaymentListItem;
        set
        {
            if (_selectedPaymentListItem == value)
            {
                return;
            }

            _selectedPaymentListItem = value;
            foreach (var payment in Payments)
            {
                payment.IsSelected = payment == value;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedPayment));
        }
    }

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
    public ICommand EditCurrentOrderQuantityCommand { get; }
    public ICommand EditCurrentOrderPriceCommand { get; }
    public ICommand EditCurrentOrderTotalCommand { get; }
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
    public ICommand PreviousProductPageCommand { get; }
    public ICommand NextProductPageCommand { get; }
    public ICommand RefreshOrdersCommand { get; }
    public ICommand ClearOrderSearchCommand { get; }
    public ICommand SelectOrderListItemCommand { get; }
    public ICommand SetOrderFilterCommand { get; }
    public ICommand PreviousOrderPageCommand { get; }
    public ICommand NextOrderPageCommand { get; }
    public ICommand OpenSelectedOrderCommand { get; }
    public ICommand PaySelectedOrderCommand { get; }
    public ICommand PrintSelectedOrderCommand { get; }
    public ICommand DeleteSelectedOrderCommand { get; }
    public ICommand EditSelectedOrderQuantityCommand { get; }
    public ICommand EditSelectedOrderPriceCommand { get; }
    public ICommand EditSelectedOrderTotalCommand { get; }
    public ICommand RefreshPaymentsCommand { get; }
    public ICommand ClearPaymentSearchCommand { get; }
    public ICommand SelectPaymentListItemCommand { get; }
    public ICommand AddCustomerInPaymentCommand { get; }
    public ICommand AddCustomerOutPaymentCommand { get; }
    public ICommand AddCashInPaymentCommand { get; }
    public ICommand AddCashOutPaymentCommand { get; }
    public ICommand PreviousPaymentPageCommand { get; }
    public ICommand NextPaymentPageCommand { get; }

    public SalesViewModel()
    {
        AddProductCommand = new RelayCommand<Product>(AddProduct);
        SelectCategoryCommand = new RelayCommand<Category>(category => SelectedCategory = category);
        CreateNewOrderCommand = new RelayCommand(CreateNewOrder);
        SelectOrderCommand = new RelayCommand<Order>(order => CurrentOrder = order);
        IncreaseQuantityCommand = new RelayCommand<OrderItem>(IncreaseQuantity);
        DecreaseQuantityCommand = new RelayCommand<OrderItem>(DecreaseQuantity);
        RemoveOrderItemCommand = new RelayCommand<OrderItem>(RemoveOrderItem);
        EditCurrentOrderQuantityCommand = new RelayCommand<OrderItem>(EditCurrentOrderQuantity);
        EditCurrentOrderPriceCommand = new RelayCommand<OrderItem>(EditCurrentOrderPrice);
        EditCurrentOrderTotalCommand = new RelayCommand<OrderItem>(EditCurrentOrderTotal);
        ClearOrderCommand = new RelayCommand(ClearOrder);
        HoldOrderCommand = new RelayCommand(HoldOrder);
        PayCommand = new RelayCommand(Pay);
        SelectOrderTypeCommand = new RelayCommand<string>(type => SelectedOrderType = type ?? "В зале");
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarOpen = !IsSidebarOpen);
        NavigateCommand = new RelayCommand<string>(Navigate);
        OpenProfileCommand = new RelayCommand(OpenUserSettings);
        OpenSettingsCommand = new RelayCommand(OpenUserSettings);
        LogoutCommand = new RelayCommand(Logout);
        SelectPaymentTypeCommand = new RelayCommand<string>(type => SelectedPaymentType = type ?? "Наличные");
        ToggleProductViewCommand = new RelayCommand(() => IsTableView = !IsTableView);
        AddNoteCommand = new RelayCommand(EditOrderNote);
        EditDiscountCommand = new RelayCommand(EditDiscount);
        ShowCustomerCommand = new RelayCommand(SelectOrderCustomer);
        ShowReceiptCommand = new RelayCommand(PrintCurrentReceipt);
        ShowOrderActionsCommand = new RelayCommand(() => { });

        PreviousProductPageCommand = new RelayCommand(() => ChangeProductPage(-1));
        NextProductPageCommand = new RelayCommand(() => ChangeProductPage(1));
        RefreshOrdersCommand = new RelayCommand(RefreshOrders);
        ClearOrderSearchCommand = new RelayCommand(() => OrderSearchText = string.Empty);
        SelectOrderListItemCommand = new RelayCommand<OrderListItem>(item => SelectedOrderListItem = item);
        SetOrderFilterCommand = new RelayCommand<string>(filter => OrderFilter = string.IsNullOrWhiteSpace(filter) ? "open" : filter);
        PreviousOrderPageCommand = new RelayCommand(() => ChangeOrderPage(-1));
        NextOrderPageCommand = new RelayCommand(() => ChangeOrderPage(1));
        OpenSelectedOrderCommand = new RelayCommand(OpenSelectedOrderInCash);
        PaySelectedOrderCommand = new RelayCommand(PaySelectedOrder);
        PrintSelectedOrderCommand = new RelayCommand(PrintSelectedOrder);
        DeleteSelectedOrderCommand = new RelayCommand(DeleteSelectedOrder);
        EditSelectedOrderQuantityCommand = new RelayCommand<OrderItem>(EditSelectedOrderQuantity);
        EditSelectedOrderPriceCommand = new RelayCommand<OrderItem>(EditSelectedOrderPrice);
        EditSelectedOrderTotalCommand = new RelayCommand<OrderItem>(EditSelectedOrderTotal);
        RefreshPaymentsCommand = new RelayCommand(RefreshPayments);
        ClearPaymentSearchCommand = new RelayCommand(() => PaymentSearchText = string.Empty);
        SelectPaymentListItemCommand = new RelayCommand<PaymentListItem>(item => SelectedPaymentListItem = item);
        AddCustomerInPaymentCommand = new RelayCommand(() => AddDdsOperation(DdsOperationTypes.CustomerIn));
        AddCustomerOutPaymentCommand = new RelayCommand(() => AddDdsOperation(DdsOperationTypes.CustomerOut));
        AddCashInPaymentCommand = new RelayCommand(() => AddDdsOperation(DdsOperationTypes.CashIn));
        AddCashOutPaymentCommand = new RelayCommand(() => AddDdsOperation(DdsOperationTypes.CashOut));
        PreviousPaymentPageCommand = new RelayCommand(() => ChangePaymentPage(-1));
        NextPaymentPageCommand = new RelayCommand(() => ChangePaymentPage(1));

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
                Name = "Все товары",
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
        if (section == "Заказы")
        {
            RefreshOrders();
        }
        else if (section == "Платежи")
        {
            RefreshPayments();
        }
    }

    private void RefreshPayments()
    {
        try
        {
            var selectedId = SelectedPaymentListItem?.Id;
            var peoples = _databaseService.GetPeoplesAsync().GetAwaiter().GetResult()
                .ToDictionary(item => item.Id, item => item.Name ?? string.Empty);
            var cashes = _databaseService.GetCashesAsync().GetAwaiter().GetResult()
                .ToDictionary(item => item.Id, item => item.Name ?? string.Empty);
            var articles = _databaseService.GetArticlesAsync().GetAwaiter().GetResult()
                .ToDictionary(item => item.Id, item => item.Name);
            _allPayments = _databaseService.GetDdsOperationsAsync().GetAwaiter().GetResult()
                .Select(item => new PaymentListItem(item, GetCustomerName(item.PeopleId, peoples), GetCashName(item.CashId, cashes), GetArticleName(item.ArticleId, articles)))
                .OrderByDescending(item => item.Id)
                .ToList();

            ApplyPaymentFilter(selectedId);
            OnPropertyChanged(nameof(PaymentsSummary));
        }
        catch (Exception ex)
        {
            AppDialogWindow.ShowError($"Не удалось загрузить платежи.\n{ex.Message}");
        }
    }

    private void ApplyPaymentFilter(int? preferredSelectedId = null)
    {
        var selectedId = preferredSelectedId ?? SelectedPaymentListItem?.Id;
        var query = PaymentSearchText.Trim();
        var filtered = _allPayments
            .Where(item => string.IsNullOrWhiteSpace(query) || MatchesPaymentSearch(item, query))
            .ToList();

        _paymentTotalCount = filtered.Count;
        _paymentIncomeTotal = filtered
            .Where(item => item.IsIncome)
            .Sum(item => item.Amount);
        _paymentExpenseTotal = filtered
            .Where(item => !item.IsIncome)
            .Sum(item => item.Amount);
        var totalPages = PaymentTotalPages;
        if (_paymentPage > totalPages)
        {
            _paymentPage = totalPages;
        }

        Payments.Clear();
        foreach (var item in filtered.Skip((_paymentPage - 1) * PaymentPageSize).Take(PaymentPageSize))
        {
            Payments.Add(item);
        }

        SelectedPaymentListItem = Payments.FirstOrDefault(item => item.Id == selectedId) ?? Payments.FirstOrDefault();
        OnPropertyChanged(nameof(PaymentsSummary));
        OnPropertyChanged(nameof(PaymentPage));
        OnPropertyChanged(nameof(PaymentTotalPages));
        OnPropertyChanged(nameof(PaymentPageText));
        OnPropertyChanged(nameof(IsPaymentPagerVisible));
        OnPropertyChanged(nameof(PaymentIncomeTotalText));
        OnPropertyChanged(nameof(PaymentExpenseTotalText));
    }

    private static bool MatchesPaymentSearch(PaymentListItem item, string query)
    {
        return item.NumberText.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.DateText.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.OperationText.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.CustomerName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.CashName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.ArticleName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            item.AmountText.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private void ChangePaymentPage(int delta)
    {
        var nextPage = Math.Clamp(_paymentPage + delta, 1, PaymentTotalPages);
        if (nextPage == _paymentPage)
        {
            return;
        }

        _paymentPage = nextPage;
        ApplyPaymentFilter();
    }

    private void AddDdsOperation(string operationType)
    {
        var window = new DdsOperationWindow(operationType)
        {
            Owner = Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true || window.Operation is null)
        {
            return;
        }

        try
        {
            _databaseService.AddDdsOperationAsync(window.Operation).GetAwaiter().GetResult();
            RefreshPayments();
        }
        catch (Exception ex)
        {
            AppDialogWindow.ShowError($"Не удалось сохранить платеж.\n{ex.Message}");
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

    private void OpenUserSettings()
    {
        var user = App.CurrentUser;
        if (user is null)
        {
            AppDialogWindow.ShowError("Пользователь не выбран. Войдите в программу заново.", "Настройки пользователя");
            return;
        }

        var window = new UserSettingsWindow(user)
        {
            Owner = Application.Current?.MainWindow
        };
        window.ShowDialog();
    }

    private void Logout()
    {
        if (!AppDialogWindow.Confirm(
                "Выйти из текущей смены и вернуться на экран авторизации?",
                "Выход",
                "Выйти",
                "Отмена"))
        {
            return;
        }

        App.ShowLoginWindow();
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
            TouchOrderItem(existing);
        }
        else
        {
            var item = new OrderItem
            {
                Product = product,
                ProductId = product.Id,
                Quantity = 1,
                Price = product.Price
            };
            CurrentOrder.Items.Add(item);
            TouchOrderItem(item);
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

    private void EditCurrentOrderQuantity(OrderItem? item)
    {
        EditCurrentOrderItem(item, "Изменить количество", current => current.Quantity.ToString(CultureInfo.CurrentCulture), value =>
        {
            var quantity = (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);
            return Math.Max(1, quantity);
        }, allowDecimal: false, applyValue: (orderItem, value) => orderItem.Quantity = Math.Max(1, (int)Math.Round(value, 0, MidpointRounding.AwayFromZero)));
    }

    private void EditCurrentOrderPrice(OrderItem? item)
    {
        EditCurrentOrderItem(item, "Изменить цену", current => (current.Price > 0 ? current.Price : current.Product.Price).ToString("0.##", CultureInfo.CurrentCulture), value =>
        {
            var price = Math.Max(0, value);
            return price;
        }, applyValue: (orderItem, value) => orderItem.Price = value);
    }

    private void EditCurrentOrderTotal(OrderItem? item)
    {
        EditCurrentOrderItem(item, "Изменить сумму", current => current.Total.ToString("0.##", CultureInfo.CurrentCulture), value =>
        {
            var total = Math.Max(0, value);
            return total;
        }, applyValue: (orderItem, value) =>
        {
            var unitPrice = orderItem.Price > 0 ? orderItem.Price : orderItem.Product.Price;
            if (unitPrice <= 0)
            {
                orderItem.Quantity = 1;
                orderItem.Price = value;
                return;
            }

            var quantity = (int)Math.Round(value / unitPrice, MidpointRounding.AwayFromZero);
            orderItem.Quantity = Math.Max(1, quantity);
        });
    }

    private void ClearOrder()
    {
        if (CurrentOrder is null || CurrentOrder.Items.Count == 0)
        {
            return;
        }

        if (AppDialogWindow.Confirm("Очистить текущий чек?"))
        {
            var clearedOrder = CurrentOrder;
            OpenOrders.Remove(clearedOrder);
            _databaseService.DeleteOpenOrderAsync(clearedOrder.Number).GetAwaiter().GetResult();
            CurrentOrder = GetOrCreateEmptyOrder(clearedOrder);
            RefreshProductQuantities();
        }
    }

    private void HoldOrder()
    {
        if (CurrentOrder is null || CurrentOrder.Items.Count == 0)
        {
            return;
        }

        var heldOrder = CurrentOrder;
        if (!OpenOrders.Contains(heldOrder))
        {
            OpenOrders.Add(heldOrder);
        }

        SaveCurrentOrder(heldOrder);
        CurrentOrder = GetOrCreateEmptyOrder(heldOrder);
        RefreshProductQuantities();
        AppDialogWindow.ShowInfo($"{heldOrder.DisplayName} отложен.");
    }

    private void Pay()
    {
        if (CurrentOrder is null || CurrentOrder.Items.Count == 0)
        {
            return;
        }

        var window = new PaymentWindow(
            CurrentOrder.Subtotal,
            SelectedPaymentType,
            CurrentOrder.Discount,
            CurrentOrder.OrderType,
            CurrentOrder.PeopleId,
            CurrentOrder.Note)
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
            SelectedOrderType = window.OrderType;
            CurrentOrder.Discount = window.DiscountAmount;
            CurrentOrder.PeopleId = window.PeopleId;
            CurrentOrder.CashId = window.CashId;
            CurrentOrder.SummaPay = window.ReceivedAmount;
            CurrentOrder.Note = window.Note;
            CurrentOrder.RefreshTotals();
            SaveCurrentOrder();
            _databaseService.MarkOrderPaidAsync(CurrentOrder, window.PaymentLines).GetAwaiter().GetResult();
            if (window.PrintReceipt)
            {
                PrintReceipt(CurrentOrder);
            }

            SyncAfterSaleIfEnabled();

            var paidOrder = CurrentOrder;
            OpenOrders.Remove(paidOrder);
            CurrentOrder = GetOrCreateEmptyOrder(paidOrder);
            RefreshProductQuantities();
        }
    }

    private void CreateNewOrder()
    {
        if (CurrentOrder is not null && CurrentOrder.Items.Count == 0)
        {
            return;
        }

        CurrentOrder = GetOrCreateEmptyOrder(CurrentOrder);
        RefreshProductQuantities();
    }

    private void RefreshOrders()
    {
        try
        {
            var selectedNumber = SelectedOrderListItem?.Number;
            var customers = _databaseService.GetPeoplesAsync().GetAwaiter().GetResult()
                .ToDictionary(customer => customer.Id, customer => customer.Name ?? string.Empty);
            var query = OrderSearchText.Trim();
            _allOrderItems = _databaseService.GetRecentOrdersAsync(_allProducts).GetAwaiter().GetResult()
                .Where(order => MatchesOrderFilter(order))
                .Select(order => new OrderListItem(order, GetCustomerName(order.PeopleId, customers)))
                .Where(item => MatchesOrderSearch(item, query))
                .OrderByDescending(item => item.Date)
                .ThenByDescending(item => item.Number)
                .ToList();

            ApplyOrderPage(selectedNumber);
        }
        catch (Exception ex)
        {
            AppDialogWindow.ShowError($"Не удалось загрузить заказы.\n{ex.Message}");
        }
    }

    private void ApplyOrderPage(int? preferredSelectedNumber = null)
    {
        var selectedNumber = preferredSelectedNumber ?? SelectedOrderListItem?.Number;
        _orderTotalCount = _allOrderItems.Count;
        var totalPages = OrderTotalPages;
        if (_orderPage > totalPages)
        {
            _orderPage = totalPages;
        }

        Orders.Clear();
        foreach (var item in _allOrderItems.Skip((_orderPage - 1) * OrderPageSize).Take(OrderPageSize))
        {
            Orders.Add(item);
        }

        SelectedOrderListItem = Orders.FirstOrDefault(item => item.Number == selectedNumber) ?? Orders.FirstOrDefault();
        OnPropertyChanged(nameof(OrdersSummary));
        OnPropertyChanged(nameof(OrderPage));
        OnPropertyChanged(nameof(OrderTotalPages));
        OnPropertyChanged(nameof(OrderPageText));
        OnPropertyChanged(nameof(IsOrderPagerVisible));
    }

    private void ChangeOrderPage(int delta)
    {
        var nextPage = Math.Clamp(_orderPage + delta, 1, OrderTotalPages);
        if (nextPage == _orderPage)
        {
            return;
        }

        _orderPage = nextPage;
        ApplyOrderPage();
    }

    private bool MatchesOrderFilter(Order order)
    {
        return OrderFilter switch
        {
            "paid" => order.Status == "paid",
            "today" => order.Date.Date == DateTime.Today,
            "all" => true,
            _ => order.Status == "open"
        };
    }

    private static bool MatchesOrderSearch(OrderListItem item, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        return item.NumberText.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.CustomerName.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.StatusText.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.OrderType.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.Note.Contains(query, StringComparison.OrdinalIgnoreCase)
            || item.TotalText.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void OpenSelectedOrderInCash()
    {
        if (SelectedOrderListItem?.Order is not { } order)
        {
            return;
        }

        if (order.Status != "open")
        {
            AppDialogWindow.ShowInfo("Оплаченный заказ можно только посмотреть или напечатать.");
            return;
        }

        var existing = OpenOrders.FirstOrDefault(openOrder => openOrder.Number == order.Number);
        if (existing is null)
        {
            OpenOrders.Add(order);
            existing = order;
        }

        CurrentOrder = existing;
        ActiveSection = "Касса";
        RefreshProductQuantities();
    }

    private void PaySelectedOrder()
    {
        OpenSelectedOrderInCash();
        if (ActiveSection == "Касса")
        {
            Pay();
            RefreshOrders();
        }
    }

    private void PrintSelectedOrder()
    {
        if (SelectedOrderListItem?.Order is not { } order)
        {
            return;
        }

        PrintReceipt(order);
    }

    private void DeleteSelectedOrder()
    {
        if (SelectedOrderListItem?.Order is not { } order)
        {
            return;
        }

        if (order.Status != "open")
        {
            AppDialogWindow.ShowInfo("Оплаченный заказ удалить нельзя.");
            return;
        }

        if (!AppDialogWindow.Confirm($"Удалить {order.DisplayName}?", "Удаление заказа"))
        {
            return;
        }

        OpenOrders.Remove(OpenOrders.FirstOrDefault(openOrder => openOrder.Number == order.Number) ?? order);
        if (CurrentOrder?.Number == order.Number)
        {
            CurrentOrder = GetOrCreateEmptyOrder(order);
        }

        _databaseService.DeleteOpenOrderAsync(order.Number).GetAwaiter().GetResult();
        RefreshOrders();
        RefreshProductQuantities();
    }

    private void EditSelectedOrderQuantity(OrderItem? item)
    {
        EditSelectedOrderItem(item, "Изменить количество", current => current.Quantity.ToString(CultureInfo.CurrentCulture), value =>
        {
            var quantity = (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);
            return Math.Max(1, quantity);
        }, applyValue: (orderItem, value) => orderItem.Quantity = Math.Max(1, (int)Math.Round(value, 0, MidpointRounding.AwayFromZero)));
    }

    private void EditSelectedOrderPrice(OrderItem? item)
    {
        EditSelectedOrderItem(item, "Изменить цену", current => (current.Price > 0 ? current.Price : current.Product.Price).ToString("0.##", CultureInfo.CurrentCulture), value =>
        {
            var price = Math.Max(0, value);
            return price;
        }, applyValue: (orderItem, value) => orderItem.Price = value);
    }

    private void EditSelectedOrderTotal(OrderItem? item)
    {
        EditSelectedOrderItem(item, "Изменить сумму", current => current.Total.ToString("0.##", CultureInfo.CurrentCulture), value =>
        {
            var total = Math.Max(0, value);
            return total;
        }, applyValue: (orderItem, value) =>
        {
            var unitPrice = orderItem.Price > 0 ? orderItem.Price : orderItem.Product.Price;
            if (unitPrice <= 0)
            {
                orderItem.Quantity = 1;
                orderItem.Price = value;
                return;
            }

            var quantity = (int)Math.Round(value / unitPrice, MidpointRounding.AwayFromZero);
            orderItem.Quantity = Math.Max(1, quantity);
        });
    }

    private void EditSelectedOrderItem(
        OrderItem? item,
        string title,
        Func<OrderItem, string> initialValueFactory,
        Func<decimal, decimal> normalizeValue,
        Action<OrderItem, decimal> applyValue)
    {
        if (item is null || SelectedOrderListItem?.Order is not { } order)
        {
            return;
        }

        if (order.Status != "open")
        {
            AppDialogWindow.ShowInfo("Редактировать можно только открытый чек.");
            return;
        }

        var window = new OrderValueInputWindow(title, initialValueFactory(item))
        {
            Owner = Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true)
        {
            return;
        }

        var newValue = normalizeValue(window.Value);
        applyValue(item, newValue);
        order.RefreshTotals();
        SaveCurrentOrder(order);
        RefreshOrders();
        RefreshProductQuantities();
    }

    private void EditCurrentOrderItem(
        OrderItem? item,
        string title,
        Func<OrderItem, string> initialValueFactory,
        Func<decimal, decimal> normalizeValue,
        Action<OrderItem, decimal> applyValue,
        bool allowDecimal = true)
    {
        if (item is null || CurrentOrder is not { } order)
        {
            return;
        }

        if (order.Status != "open")
        {
            AppDialogWindow.ShowInfo("Редактировать можно только открытый чек.");
            return;
        }

        var window = new OrderValueInputWindow(title, initialValueFactory(item), allowDecimal)
        {
            Owner = Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true)
        {
            return;
        }

        var newValue = normalizeValue(window.Value);
        applyValue(item, newValue);
        order.RefreshTotals();
        SaveCurrentOrder(order);
        RefreshOrders();
        RefreshProductQuantities();
    }

    private void SelectOrderCustomer()
    {
        if (CurrentOrder is null)
        {
            return;
        }

        var window = new CustomerSelectWindow(CurrentOrder.PeopleId)
        {
            Owner = Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true || window.SelectedCustomer is null)
        {
            return;
        }

        CurrentOrder.PeopleId = window.SelectedCustomer.Id;
        SaveCurrentOrder();
        AppDialogWindow.ShowSuccess($"Клиент выбран: {GetCustomerName(CurrentOrder.PeopleId)}");
    }

    private void EditOrderNote()
    {
        if (CurrentOrder is null)
        {
            return;
        }

        var window = new OrderNoteWindow(CurrentOrder.Note)
        {
            Owner = Application.Current?.MainWindow
        };

        if (window.ShowDialog() != true)
        {
            return;
        }

        CurrentOrder.Note = window.Note;
        SaveCurrentOrder();
    }

    private Order GetOrCreateEmptyOrder(Order? exclude = null)
    {
        var emptyOrder = OpenOrders.FirstOrDefault(order => order != exclude && order.Status == "open" && order.Items.Count == 0);
        return emptyOrder ?? CreateOrder();
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

    private void PrintCurrentReceipt()
    {
        if (CurrentOrder is null || CurrentOrder.Items.Count == 0)
        {
            return;
        }

        PrintReceipt(CurrentOrder);
    }

    private static void SyncAfterSaleIfEnabled()
    {
        var userSettings = ParseCurrentUserSettings();
        if (!userSettings.SyncAfterSale)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await new SyncService(new AppSettingsService().Load()).SyncPendingSalesAsync();
            }
            catch
            {
            }
        });
    }

    private static UserSettings ParseCurrentUserSettings()
    {
        var json = App.CurrentUser?.Settings;
        if (string.IsNullOrWhiteSpace(json))
        {
            return new UserSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    private void PrintReceipt(Order order)
    {
        try
        {
            _printService.PrintReceipt(BuildReceiptText(order), GetReceiptPrinterName());
        }
        catch (Exception ex)
        {
            AppDialogWindow.ShowError($"Не удалось напечатать чек.\n{ex.Message}");
        }
    }

    private string BuildReceiptText(Order order)
    {
        var builder = new StringBuilder();
        builder.AppendLine("TIJORAT.PRO");
        builder.AppendLine(order.DisplayName);
        builder.AppendLine(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
        builder.AppendLine($"Клиент: {GetCustomerName(order.PeopleId)}");
        builder.AppendLine($"Тип: {order.OrderType}");
        if (!string.IsNullOrWhiteSpace(order.Note))
        {
            builder.AppendLine($"Примечание: {order.Note}");
        }

        builder.AppendLine(new string('-', 32));
        foreach (var item in order.Items)
        {
            var name = string.IsNullOrWhiteSpace(item.Product?.Name) ? $"Товар {item.ProductId}" : item.Product.Name;
            builder.AppendLine(name);
            builder.AppendLine($"{item.Quantity} x {item.Price:0.##} = {item.Total:0.##}");
        }

        builder.AppendLine(new string('-', 32));
        builder.AppendLine($"Итого: {order.Subtotal:0.##}");
        builder.AppendLine($"Скидка: {order.Discount:0.##}");
        builder.AppendLine($"К оплате: {order.Total:0.##}");
        builder.AppendLine($"Получено: {order.SummaPay:0.##}");
        builder.AppendLine($"Сдача: {Math.Max(0, order.SummaPay - order.Total):0.##}");
        builder.AppendLine();
        builder.AppendLine("Спасибо за покупку!");
        return builder.ToString();
    }

    private string GetCustomerName(int peopleId)
    {
        try
        {
            var customer = _databaseService.GetPeoplesAsync().GetAwaiter().GetResult()
                .FirstOrDefault(people => people.Id == peopleId);

            if (!string.IsNullOrWhiteSpace(customer?.Name))
            {
                return customer.Name;
            }
        }
        catch
        {
        }

        return peopleId > 1 ? $"Клиент №{peopleId}" : "Розничный покупатель";
    }

    private static string GetCustomerName(int peopleId, IReadOnlyDictionary<int, string> customers)
    {
        return customers.TryGetValue(peopleId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : peopleId > 1 ? $"Клиент №{peopleId}" : "Розничный покупатель";
    }

    private static string GetCashName(int cashId, IReadOnlyDictionary<int, string> cashes)
    {
        return cashes.TryGetValue(cashId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : $"Касса №{cashId}";
    }

    private static string GetArticleName(int articleId, IReadOnlyDictionary<int, string> articles)
    {
        return articles.TryGetValue(articleId, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : articleId > 0 ? $"Статья №{articleId}" : "-";
    }

    private static string? GetReceiptPrinterName()
    {
        var userSettingsJson = App.CurrentUser?.Settings;
        if (!string.IsNullOrWhiteSpace(userSettingsJson))
        {
            try
            {
                var userSettings = JsonSerializer.Deserialize<UserSettings>(userSettingsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (!string.IsNullOrWhiteSpace(userSettings?.PrinterNameDefault))
                {
                    return userSettings.PrinterNameDefault;
                }
            }
            catch
            {
            }
        }

        return new AppSettingsService().Load().PrinterNameDefault;
    }

    private void FilterProducts()
    {
        Products.Clear();
        var query = SearchText.Trim();
        var filtered = _allProducts.Where(product =>
            (!IsCardView || product.PosView == 1) &&
            (SelectedCategory is null || SelectedCategory.Id == 0 || product.CategoryId == SelectedCategory.Id) &&
            (string.IsNullOrWhiteSpace(query) ||
                product.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(product.Barcode) && product.Barcode.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(product.Sku) && product.Sku.Contains(query, StringComparison.OrdinalIgnoreCase))));

        _productTotalCount = filtered.Count();
        var totalPages = ProductTotalPages;
        if (_productPage > totalPages)
        {
            _productPage = totalPages;
        }

        var pageSize = ProductPageSize;
        foreach (var product in filtered.Skip((_productPage - 1) * pageSize).Take(pageSize))
        {
            Products.Add(product);
        }

        OnPropertyChanged(nameof(ProductPage));
        OnPropertyChanged(nameof(ProductTotalPages));
        OnPropertyChanged(nameof(ProductPageText));
        OnPropertyChanged(nameof(IsProductPagerVisible));
    }

    private void RefreshProductQuantities()
    {
        var selectedQuantities = CurrentOrder?.Items
            .GroupBy(item => item.Product.Id)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity)) ?? new Dictionary<int, int>();

        foreach (var product in _allProducts)
        {
            product.SelectedQuantity = selectedQuantities.TryGetValue(product.Id, out var quantity)
                ? quantity
                : 0;
        }
    }

    private void TouchOrderItem(OrderItem item)
    {
        OrderItemTouched?.Invoke(this, item);
    }

    private void ChangeProductPage(int delta)
    {
        var nextPage = Math.Clamp(_productPage + delta, 1, ProductTotalPages);
        if (nextPage == _productPage)
        {
            return;
        }

        _productPage = nextPage;
        FilterProducts();
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

public class OrderListItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public OrderListItem(Order order, string customerName)
    {
        Order = order;
        CustomerName = customerName;
        Order.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Order.Subtotal) or nameof(Order.Total) or nameof(Order.Status) or nameof(Order.SyncStatus) or nameof(Order.OrderType) or nameof(Order.Note) or nameof(Order.Date))
            {
                OnPropertyChanged(nameof(ItemsCount));
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(TotalText));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(StatusBackground));
                OnPropertyChanged(nameof(SyncText));
                OnPropertyChanged(nameof(SyncBrush));
                OnPropertyChanged(nameof(SyncBackground));
                OnPropertyChanged(nameof(OrderType));
                OnPropertyChanged(nameof(Note));
                OnPropertyChanged(nameof(DateText));
            }
        };
    }

    public Order Order { get; }
    public int Number => Order.Number;
    public string NumberText => Order.DisplayName;
    public DateTime Date => Order.Date;
    public string DateText => Order.Date.ToString("dd.MM.yyyy HH:mm");
    public string CustomerName { get; }
    public string OrderType => Order.OrderType;
    public string Note => Order.Note ?? string.Empty;
    public int ItemsCount => Order.Items.Sum(item => item.Quantity);
    public decimal Subtotal => Order.Subtotal;
    public decimal Discount => Order.Discount;
    public decimal Total => Order.Total;
    public string TotalText => $"{Total:0.00} c";
    public string StatusText => Order.Status == "paid" ? "Оплачен" : "Открыт";
    public string StatusBrush => Order.Status == "paid" ? "#16A34A" : "#F59E0B";
    public string StatusBackground => Order.Status == "paid" ? "#ECFDF3" : "#FFF7E6";
    public string SyncText => Order.SyncStatus switch
    {
        1 => "Синх.",
        2 => "Ошибка",
        _ => "Ожидает"
    };
    public string SyncBrush => Order.SyncStatus switch
    {
        1 => "#16A34A",
        2 => "#FF2B22",
        _ => "#F59E0B"
    };
    public string SyncBackground => Order.SyncStatus switch
    {
        1 => "#ECFDF3",
        2 => "#FFF1F0",
        _ => "#FFF7E6"
    };
    public bool IsOpen => Order.Status == "open";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class PaymentListItem : INotifyPropertyChanged
{
    private bool _isSelected;

    public PaymentListItem(Dds payment, string customerName, string cashName, string articleName)
    {
        Payment = payment;
        CustomerName = customerName;
        CashName = cashName;
        ArticleName = articleName;
    }

    public Dds Payment { get; }
    public int Id => Payment.Id;
    public int DocId => Payment.DocId;
    public decimal Amount => Math.Abs(Payment.Summa);
    public bool IsIncome => DdsOperationTypes.IsIncome(Payment.OrderType);
    public string NumberText => $"№{Payment.Id}";
    public string DateText => FormatDate(Payment.Date);
    public string OperationText => DdsOperationTypes.GetTitle(Payment.OrderType);
    public string CustomerName { get; }
    public string CashName { get; }
    public string ArticleName { get; }
    public string Description => Payment.Description ?? string.Empty;
    public string DirectionText => IsIncome ? "Приход" : "Расход";
    public string DirectionBrush => IsIncome ? "#16A34A" : "#FF2B22";
    public string DirectionBackground => IsIncome ? "#ECFDF3" : "#FFF1F0";
    public string AmountText => $"{(IsIncome ? "+" : "-")}{Payment.Summa:0.00} c";
    public string SyncText => Payment.SyncStatus switch
    {
        1 => "Синх.",
        2 => "Ошибка",
        _ => "Ожидает"
    };

    private static string FormatDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var formats = new[] { "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd H:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy H:mm" };
        return DateTime.TryParseExact(value.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
               DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date) ||
               DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)
            ? date.ToString("dd.MM.yyyy HH:mm")
            : value;
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

