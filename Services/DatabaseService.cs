using FFPOS.Data;
using FFPOS.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System.IO;

namespace FFPOS.Services;

public sealed class DatabaseService
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

    private readonly AppSettingsService _settingsService = new();
    private readonly AppActivationSettings _settings;
    private readonly string _databasePath;
    private readonly DbContextOptions<AppDbContext> _options;

    public DatabaseService()
    {
        _settings = _settingsService.Load();

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var directory = Path.Combine(appData, "TIJORAT PRO");
        _databasePath = Path.Combine(directory, "pos.sqlite3");

        var builder = new DbContextOptionsBuilder<AppDbContext>();
        if (IsMySql)
        {
            builder.UseMySql(BuildMySqlConnectionString(includeDatabase: true), new MySqlServerVersion(new Version(8, 0, 36)));
        }
        else
        {
            builder.UseSqlite(BuildSqliteConnectionString());
        }

        _options = builder.Options;
    }

    public string DatabasePath => _databasePath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (IsMySql)
        {
            await EnsureMySqlDatabaseExistsAsync(cancellationToken);
        }

        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureSalesTablesAsync(context, cancellationToken);
        await EnsureSalesColumnsAsync(context, cancellationToken);
        await NormalizeSalesCodeValuesAsync(context, cancellationToken);
        await EnsurePeopleColumnsAsync(context, cancellationToken);
        await EnsureArticlesTableAsync(context, cancellationToken);
        await EnsureDdsColumnsAsync(context, cancellationToken);
        await EnsureShiftsTableAsync(context, cancellationToken);

        if (!IsMySql)
        {
            await context.Database.ExecuteSqlRawAsync("""
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                PRAGMA foreign_keys=ON;
                PRAGMA busy_timeout=5000;
                """, cancellationToken);

            await MigrateUsersPasswordToPincodeAsync(context, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Store>> GetStoresAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Stores
            .AsNoTracking()
            .Where(store => store.Status == 1)
            .OrderBy(store => store.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Categories
            .AsNoTracking()
            .Where(category => category.IsActiveValue == 1)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Products
            .AsNoTracking()
            .Where(product => product.Status == 1)
            .OrderBy(product => product.SortOrder)
            .ThenBy(product => product.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<User>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Users
            .AsNoTracking()
            .Where(user => user.Status == 1)
            .OrderBy(user => user.Name ?? user.Username)
            .ThenBy(user => user.Username)
            .ThenBy(user => user.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Stock>> GetStocksAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Stoks
            .AsNoTracking()
            .Where(stock => stock.Status == 1)
            .OrderBy(stock => stock.Name)
            .ThenBy(stock => stock.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Cash>> GetCashesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Cashes
            .AsNoTracking()
            .Where(cash => cash.Status == 1)
            .OrderBy(cash => cash.Name)
            .ThenBy(cash => cash.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<People>> GetPeoplesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Peoples
            .AsNoTracking()
            .Where(people => people.Status == 1)
            .OrderBy(people => people.Id == 1 ? 0 : 1)
            .ThenBy(people => people.Name)
            .ThenBy(people => people.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Article>> GetArticlesAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Articles
            .AsNoTracking()
            .Where(article => article.Check == 1)
            .OrderBy(article => article.ParentId)
            .ThenBy(article => article.Name)
            .ThenBy(article => article.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Dds>> GetDdsOperationsAsync(
        int take = 500,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Dds
            .AsNoTracking()
            .OrderByDescending(item => item.Date)
            .ThenByDescending(item => item.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Shift?> GetOpenShiftAsync(
        int storeId,
        int cashId,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Shifts
            .AsNoTracking()
            .Where(shift => shift.Status == 1 && shift.StoreId == storeId && shift.CashId == cashId && shift.ClosedAt == null)
            .OrderByDescending(shift => shift.OpenedAt)
            .ThenByDescending(shift => shift.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Shift> OpenShiftAsync(
        int storeId,
        int cashId,
        int openedByUserId,
        decimal openingBalance = 0m,
        CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            var existing = await context.Shifts
                .FirstOrDefaultAsync(
                    shift => shift.Status == 1 && shift.StoreId == storeId && shift.CashId == cashId && shift.ClosedAt == null,
                    cancellationToken);

            if (existing is not null)
            {
                return existing;
            }

            var nextId = (await context.Shifts.AsNoTracking().Select(item => (int?)item.Id).MaxAsync(cancellationToken) ?? 0) + 1;
            var now = DateTime.Now;
            var shift = new Shift
            {
                Id = nextId,
                StoreId = storeId,
                CashId = cashId,
                OpenedByUserId = openedByUserId,
                OpeningBalance = openingBalance,
                OpenedAt = now,
                ExpiresAt = now.AddHours(24),
                Status = 1
            };

            context.Shifts.Add(shift);
            await context.SaveChangesAsync(cancellationToken);
            return shift;
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<Shift?> CloseShiftAsync(
        int shiftId,
        int closedByUserId,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            var shift = await context.Shifts.FirstOrDefaultAsync(item => item.Id == shiftId, cancellationToken);
            if (shift is null || shift.ClosedAt is not null)
            {
                return shift;
            }

            var closedAt = DateTime.Now;
            var openedEpoch = new DateTimeOffset(shift.OpenedAt).ToUnixTimeSeconds();
            var closedEpoch = new DateTimeOffset(closedAt).ToUnixTimeSeconds();
            var orders = await context.Orders
                .AsNoTracking()
                .Where(order =>
                    order.StoreId == shift.StoreId &&
                    order.CashId == shift.CashId &&
                    order.Date >= shift.OpenedAt &&
                    order.Date <= closedAt)
                .ToListAsync(cancellationToken);

            var payments = await context.Dds
                .AsNoTracking()
                .Where(item =>
                    item.StoreId == shift.StoreId &&
                    item.CashId == shift.CashId &&
                    item.EventTime >= openedEpoch &&
                    item.EventTime <= closedEpoch)
                .ToListAsync(cancellationToken);

            var sales = orders.Where(order => order.Status == "paid" && !IsReturnSale(order)).ToList();
            var returns = orders.Where(IsReturnSale).ToList();
            var salePayments = sales.Sum(order => order.SummaPay > 0 ? order.SummaPay : order.Total);
            var paymentIncome = payments.Where(item => item.OrderType is DdsOperationTypes.CustomerIn or DdsOperationTypes.CashIn).Sum(item => item.Summa);
            var paymentExpense = payments.Where(item => item.OrderType is DdsOperationTypes.CustomerOut or DdsOperationTypes.CashOut).Sum(item => item.Summa);
            var cashIncome = payments.Where(item => item.OrderType == DdsOperationTypes.CashIn).Sum(item => item.Summa);
            var cashExpense = payments.Where(item => item.OrderType == DdsOperationTypes.CashOut).Sum(item => item.Summa);

            shift.SalesCount = sales.Count;
            shift.SalesTotal = sales.Sum(order => order.Total);
            shift.ReturnTotal = returns.Sum(order => order.Total);
            shift.SalePaymentTotal = salePayments;
            shift.PaymentIncomeTotal = paymentIncome;
            shift.PaymentExpenseTotal = paymentExpense;
            shift.PaymentTotal = paymentIncome - paymentExpense;
            shift.PaymentCount = payments.Count;
            shift.CashInTotal = cashIncome;
            shift.CashOutTotal = cashExpense;
            shift.ClosingBalance = shift.OpeningBalance + shift.SalePaymentTotal + shift.PaymentTotal - shift.ReturnTotal;
            shift.ClosedAt = closedAt;
            shift.ClosedByUserId = closedByUserId;
            shift.Note = string.IsNullOrWhiteSpace(note) ? shift.Note : note;
            shift.Status = 2;

            await context.SaveChangesAsync(cancellationToken);
            return shift;
        }
        finally
        {
            WriteLock.Release();
        }
    }

    private static bool IsReturnSale(Order order)
    {
        return order.SaleType == Order.SaleTypeReturn;
    }

    public async Task<Dds> AddDdsOperationAsync(Dds operation, CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            var nextId = (await context.Dds.AsNoTracking().Select(item => (int?)item.Id).MaxAsync(cancellationToken) ?? 0) + 1;
            operation.Id = operation.Id > 0 ? operation.Id : nextId;
            operation.DocId = operation.DocId > 0 ? operation.DocId : operation.Id;
            operation.StoreId = operation.StoreId > 0 ? operation.StoreId : _settings.StoreId;
            operation.UserId = operation.UserId > 0 ? operation.UserId : App.CurrentUser?.Id ?? 1;
            var userSettings = UserSettings.Parse(App.CurrentUser?.Settings);
            var defaultCashId = userSettings.DefaultCashId > 0
                ? userSettings.DefaultCashId
                : App.CurrentUser?.CashId > 0 ? App.CurrentUser.CashId : 1;
            operation.CashId = operation.CashId > 0 ? operation.CashId : defaultCashId;
            operation.PeopleId = operation.PeopleId > 0 ? operation.PeopleId : 1;
            operation.ArticleId = operation.ArticleId > 0 ? operation.ArticleId : 1;
            operation.Summa = Math.Abs(operation.Summa);
            operation.EventTime = operation.EventTime > 0 ? operation.EventTime : DateTimeOffset.Now.ToUnixTimeSeconds();
            operation.OrderType = string.IsNullOrWhiteSpace(operation.OrderType) ? DdsOperationTypes.CashIn : operation.OrderType;
            operation.Date = string.IsNullOrWhiteSpace(operation.Date) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") : operation.Date;
            operation.Status = operation.Status == 0 ? 1 : operation.Status;
            operation.SyncStatus = 0;
            operation.SyncError = null;

            context.Dds.Add(operation);
            await context.SaveChangesAsync(cancellationToken);
            return operation;
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<People> AddPeopleAsync(People people, CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            if (people.Id > 0)
            {
                var existing = await context.Peoples.FirstOrDefaultAsync(item => item.Id == people.Id, cancellationToken);
                if (existing is not null)
                {
                    existing.Name = people.Name;
                    existing.Phone = people.Phone;
                    existing.Address = people.Address;
                    existing.Balance = people.Balance;
                    existing.Status = people.Status == 0 ? 1 : people.Status;
                    await context.SaveChangesAsync(cancellationToken);
                    return existing;
                }
            }

            var nextId = (await context.Peoples.AsNoTracking().Select(item => (int?)item.Id).MaxAsync(cancellationToken) ?? 0) + 1;
            people.Id = people.Id > 0 ? people.Id : nextId;
            people.Status = people.Status == 0 ? 1 : people.Status;
            context.Peoples.Add(people);
            await context.SaveChangesAsync(cancellationToken);
            return people;
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task SaveUserSettingsAsync(User user, string settingsJson, CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            var tracked = await context.Users.FirstOrDefaultAsync(existing => existing.Id == user.Id, cancellationToken);
            if (tracked is null)
            {
                return;
            }

            tracked.StockId = user.StockId;
            tracked.CashId = user.CashId;
            tracked.Settings = settingsJson;
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(
        IReadOnlyList<Product> products,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var orders = await context.Orders
            .AsNoTracking()
            .Where(order => order.Status == "open")
            .Include(order => order.Items)
            .ThenInclude(item => item.Product)
            .OrderBy(order => order.Number)
            .ToListAsync(cancellationToken);

        var productLookup = products.ToDictionary(product => product.Id);
        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                if (item.Product is null && productLookup.TryGetValue(item.ProductId, out var product))
                {
                    item.Product = product;
                }

                if (item.Price <= 0 && item.Product is not null)
                {
                    item.Price = item.Product.Price;
                }
            }

            order.RefreshTotals();
        }

        return orders;
    }

    public async Task<IReadOnlyList<Order>> GetRecentOrdersAsync(
        IReadOnlyList<Product> products,
        int take = 500,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var orders = await context.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .ThenInclude(item => item.Product)
            .OrderByDescending(order => order.Date)
            .ThenByDescending(order => order.Number)
            .Take(take)
            .ToListAsync(cancellationToken);

        var productLookup = products.ToDictionary(product => product.Id);
        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                if (item.Product is null && productLookup.TryGetValue(item.ProductId, out var product))
                {
                    item.Product = product;
                }

                if (item.Price <= 0 && item.Product is not null)
                {
                    item.Price = item.Product.Price;
                }
            }

            order.RefreshTotals();
        }

        return orders;
    }

    public async Task<int> GetNextOrderNumberAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var max = await context.Orders.AsNoTracking().Select(order => (int?)order.Number).MaxAsync(cancellationToken);
        return (max ?? 100) + 1;
    }

    public async Task SaveOpenOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            var existing = await context.Orders
                .Include(existingOrder => existingOrder.Items)
                .FirstOrDefaultAsync(existingOrder => existingOrder.Number == order.Number, cancellationToken);

            var trackedOrder = existing ?? new Order { Number = order.Number };
            if (existing is null)
            {
                context.Orders.Add(trackedOrder);
            }

            trackedOrder.OrderType = order.OrderType;
            trackedOrder.SaleType = order.SaleType;
            trackedOrder.Status = string.IsNullOrWhiteSpace(order.Status) ? "open" : order.Status;
            trackedOrder.Discount = order.Discount;
            trackedOrder.StoreId = order.StoreId > 0 ? order.StoreId : _settings.StoreId;
            trackedOrder.StockId = order.StockId > 0 ? order.StockId : _settings.StockId;
            trackedOrder.UserId = order.UserId > 0 ? order.UserId : 1;
            trackedOrder.CashId = order.CashId > 0 ? order.CashId : 1;
            trackedOrder.PriceId = order.PriceId > 0 ? order.PriceId : _settings.PriceId;
            trackedOrder.PeopleId = order.PeopleId > 0 ? order.PeopleId : _settings.PeopleId;
            trackedOrder.Summa = order.Subtotal;
            trackedOrder.BonusSum = order.BonusSum;
            trackedOrder.SummaPay = order.SummaPay > 0 ? order.SummaPay : order.Total;
            trackedOrder.Note = order.Note;
            trackedOrder.Date = order.Date == default ? DateTime.Now : order.Date;
            trackedOrder.SyncStatus = 0;
            trackedOrder.SyncError = null;

            if (existing is not null)
            {
                trackedOrder.Items.Clear();
            }

            foreach (var sourceItem in order.Items)
            {
                var productId = sourceItem.ProductId > 0 ? sourceItem.ProductId : sourceItem.Product?.Id ?? 0;
                if (productId <= 0)
                {
                    continue;
                }

                trackedOrder.Items.Add(new OrderItem
                {
                    ProductId = productId,
                    Quantity = sourceItem.Quantity,
                    Price = sourceItem.Price > 0 ? sourceItem.Price : sourceItem.Product?.Price ?? 0m,
                    Discount = sourceItem.Discount,
                    Bonus = sourceItem.Bonus
                });
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task MarkOrderPaidAsync(
        Order order,
        IEnumerable<PaymentLine>? payments = null,
        CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            var tracked = await context.Orders
                .Include(existing => existing.Items)
                .FirstOrDefaultAsync(existing => existing.Number == order.Number, cancellationToken);
            if (tracked is null)
            {
                return;
            }

            tracked.Status = "paid";
            tracked.OrderType = order.OrderType;
            tracked.SaleType = order.SaleType;
            tracked.CashId = order.CashId > 0 ? order.CashId : tracked.CashId;
            tracked.PeopleId = order.PeopleId > 0 ? order.PeopleId : tracked.PeopleId;
            tracked.Summa = order.Subtotal;
            tracked.SummaPay = order.SummaPay > 0 ? order.SummaPay : order.Total;
            tracked.Discount = order.Discount;
            tracked.Note = order.Note;
            tracked.SyncStatus = 0;
            tracked.SyncError = null;
            SavePaymentRows(context, tracked, payments);
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task<IReadOnlyList<Order>> GetPendingSalesForSyncAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var orders = await context.Orders
            .AsNoTracking()
            .Where(order => order.Status == "paid" && order.SyncStatus != 1)
            .Include(order => order.Items)
            .ThenInclude(item => item.Product)
            .OrderBy(order => order.Date)
            .ThenBy(order => order.Number)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var order in orders)
        {
            order.RefreshTotals();
        }

        return orders;
    }

    public async Task<IReadOnlyList<Dds>> GetPaymentsForSalesAsync(
        IEnumerable<int> orderNumbers,
        CancellationToken cancellationToken = default)
    {
        var numbers = orderNumbers.Distinct().ToArray();
        if (numbers.Length == 0)
        {
            return Array.Empty<Dds>();
        }

        await using var context = CreateContext();
        return await context.Dds
            .AsNoTracking()
            .Where(payment => payment.OrderType == "salepay" && numbers.Contains(payment.DocId))
            .OrderBy(payment => payment.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkSaleSyncSucceededAsync(
        int orderNumber,
        int? serverId,
        IReadOnlyDictionary<int, int> paymentServerIds,
        CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            var order = await context.Orders.FirstOrDefaultAsync(item => item.Number == orderNumber, cancellationToken);
            if (order is not null)
            {
                order.SyncStatus = 1;
                order.ServerId = serverId ?? order.ServerId;
                order.SyncedAt = DateTime.Now;
                order.SyncError = null;
            }

            var payments = await context.Dds
                .Where(payment => payment.OrderType == "salepay" && payment.DocId == orderNumber)
                .ToListAsync(cancellationToken);
            foreach (var payment in payments)
            {
                payment.SyncStatus = 1;
                if (paymentServerIds.TryGetValue(payment.Id, out var paymentServerId))
                {
                    payment.ServerId = paymentServerId;
                }

                payment.SyncedAt = DateTime.Now;
                payment.SyncError = null;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task MarkSaleSyncFailedAsync(
        int orderNumber,
        string error,
        CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            var shortError = error.Length > 1000 ? error[..1000] : error;
            var order = await context.Orders.FirstOrDefaultAsync(item => item.Number == orderNumber, cancellationToken);
            if (order is not null)
            {
                order.SyncStatus = 2;
                order.SyncError = shortError;
            }

            var payments = await context.Dds
                .Where(payment => payment.OrderType == "salepay" && payment.DocId == orderNumber)
                .ToListAsync(cancellationToken);
            foreach (var payment in payments)
            {
                payment.SyncStatus = 2;
                payment.SyncError = shortError;
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public async Task DeleteOpenOrderAsync(int orderNumber, CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            var tracked = await context.Orders
                .Include(order => order.Items)
                .FirstOrDefaultAsync(order => order.Number == orderNumber && order.Status == "open", cancellationToken);

            if (tracked is null)
            {
                return;
            }

            context.Orders.Remove(tracked);
            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    public Task UpsertStoresAsync(IEnumerable<Store> stores, CancellationToken cancellationToken = default) =>
        UpsertEntitiesAsync(stores, nameof(Store.Id), store => store.Id, cancellationToken);

    public Task UpsertStoksAsync(IEnumerable<Stock> stoks, CancellationToken cancellationToken = default) =>
        UpsertEntitiesAsync(stoks, nameof(Stock.Id), stok => stok.Id, cancellationToken);

    public Task UpsertCashesAsync(IEnumerable<Cash> cashes, CancellationToken cancellationToken = default) =>
        UpsertEntitiesAsync(cashes, nameof(Cash.Id), cash => cash.Id, cancellationToken);

    public Task UpsertPricesAsync(IEnumerable<Price> prices, CancellationToken cancellationToken = default) =>
        UpsertEntitiesAsync(prices, nameof(Price.Id), price => price.Id, cancellationToken);

    public Task UpsertPriceDataAsync(IEnumerable<PriceData> priceData, CancellationToken cancellationToken = default) =>
        UpsertEntitiesAsync(priceData, nameof(PriceData.Id), data => data.Id, cancellationToken);

    public Task UpsertUsersAsync(IEnumerable<User> users, CancellationToken cancellationToken = default) =>
        UpsertEntitiesAsync(users, nameof(User.Id), user => user.Id, cancellationToken);

    public Task UpsertPeoplesAsync(IEnumerable<People> peoples, CancellationToken cancellationToken = default) =>
        UpsertEntitiesAsync(peoples, nameof(People.Id), people => people.Id, cancellationToken);

    public Task UpsertArticlesAsync(IEnumerable<Article> articles, CancellationToken cancellationToken = default) =>
        UpsertEntitiesAsync(articles, nameof(Article.Id), article => article.Id, cancellationToken);

    public Task UpsertCategoriesAsync(IEnumerable<Category> categories, CancellationToken cancellationToken = default)
    {
        foreach (var category in categories)
        {
            category.IconPath = string.IsNullOrWhiteSpace(category.IconPath)
                ? "/Assets/Images/default.png"
                : category.IconPath;
            category.SortOrder = category.SortOrder == 0 ? category.Id : category.SortOrder;
            category.IsActiveValue = category.Status == 1 ? 1 : 0;
        }

        return UpsertEntitiesAsync(categories, nameof(Category.Id), category => category.Id, cancellationToken);
    }

    public Task UpsertProductsAsync(IEnumerable<Product> products, CancellationToken cancellationToken = default)
    {
        foreach (var product in products)
        {
            product.ImagePath = string.IsNullOrWhiteSpace(product.ImagePath)
                ? "/Assets/Images/default.png"
                : product.ImagePath;
            product.Package = product.Package < 1 ? 1 : product.Package;
            product.PosView = product.PosView == 1 ? 1 : 0;
            product.SortOrder = product.SortOrder == 0 ? product.Id : product.SortOrder;
        }

        return UpsertEntitiesAsync(products, nameof(Product.Id), product => product.Id, cancellationToken);
    }

    private bool IsMySql => _settings.DatabaseType == 2;

    private AppDbContext CreateContext()
    {
        return new AppDbContext(_options);
    }

    private string BuildSqliteConnectionString()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };

        return builder.ToString();
    }

    private string BuildMySqlConnectionString(bool includeDatabase)
    {
        var builder = new MySqlConnectionStringBuilder
        {
            Server = _settings.MySqlHost,
            Port = (uint)(_settings.MySqlPort > 0 ? _settings.MySqlPort : 3306),
            UserID = _settings.MySqlUsername,
            Password = _settings.MySqlPassword,
            ConnectionTimeout = 5,
            SslMode = MySqlSslMode.None
        };

        if (includeDatabase)
        {
            builder.Database = _settings.MySqlDatabase;
        }

        return builder.ConnectionString;
    }

    private async Task EnsureMySqlDatabaseExistsAsync(CancellationToken cancellationToken)
    {
        var databaseName = string.IsNullOrWhiteSpace(_settings.MySqlDatabase) ? "local_db" : _settings.MySqlDatabase.Trim();
        var connectionString = BuildMySqlConnectionString(includeDatabase: false);
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE IF NOT EXISTS `{databaseName.Replace("`", "``")}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureSalesTablesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (IsMySql)
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS sales (
                    id INT NOT NULL PRIMARY KEY,
                    store_id INT NOT NULL DEFAULT 1,
                    stock_id INT NOT NULL DEFAULT 1,
                    user_id INT NOT NULL DEFAULT 1,
                    cash_id INT NOT NULL DEFAULT 1,
                    price_id INT NOT NULL DEFAULT 1,
                    people_id INT NOT NULL DEFAULT 1,
                    summa DECIMAL(18,2) NOT NULL DEFAULT 0,
                    discount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    bonussum DECIMAL(18,2) NOT NULL DEFAULT 0,
                    summapay DECIMAL(18,2) NOT NULL DEFAULT 0,
                    date DATETIME NOT NULL,
                    sale_type VARCHAR(30) NOT NULL DEFAULT 'sale',
                    type VARCHAR(50) NOT NULL DEFAULT '3',
                    status VARCHAR(30) NOT NULL DEFAULT '1',
                    sync_status INT NOT NULL DEFAULT 0,
                    server_id INT NULL,
                    synced_at DATETIME NULL,
                    sync_error TEXT NULL,
                    note TEXT NULL
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """, cancellationToken);

            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS sale_data (
                    id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    sale_id INT NOT NULL,
                    product_id INT NOT NULL,
                    quantity DECIMAL(18,3) NOT NULL DEFAULT 0,
                    price DECIMAL(18,2) NOT NULL DEFAULT 0,
                    discount DECIMAL(18,2) NOT NULL DEFAULT 0,
                    bonus DECIMAL(18,2) NOT NULL DEFAULT 0,
                    INDEX ix_sale_data_sale_id (sale_id),
                    CONSTRAINT fk_sale_data_sales_sale_id FOREIGN KEY (sale_id) REFERENCES sales(id) ON DELETE CASCADE
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """, cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "store_id", "INT NOT NULL DEFAULT 1", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "cash_id", "INT NOT NULL DEFAULT 1", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "opened_by_user_id", "INT NOT NULL DEFAULT 1", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "closed_by_user_id", "INT NULL", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "opening_balance", "DECIMAL(18,2) NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "sales_total", "DECIMAL(18,2) NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "return_total", "DECIMAL(18,2) NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "sale_payment_total", "DECIMAL(18,2) NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "payment_income_total", "DECIMAL(18,2) NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "payment_expense_total", "DECIMAL(18,2) NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "payment_total", "DECIMAL(18,2) NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "cash_in_total", "DECIMAL(18,2) NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "cash_out_total", "DECIMAL(18,2) NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "closing_balance", "DECIMAL(18,2) NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "sales_count", "INT NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "payment_count", "INT NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "opened_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "expires_at", "DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "closed_at", "DATETIME NULL", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "note", "TEXT NULL", cancellationToken);
            await EnsureMySqlColumnAsync(context, "shifts", "status", "INT NOT NULL DEFAULT 1", cancellationToken);
            return;
        }

        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS sales (
                id INTEGER NOT NULL PRIMARY KEY,
                store_id INTEGER NOT NULL DEFAULT 1,
                stock_id INTEGER NOT NULL DEFAULT 1,
                user_id INTEGER NOT NULL DEFAULT 1,
                cash_id INTEGER NOT NULL DEFAULT 1,
                price_id INTEGER NOT NULL DEFAULT 1,
                people_id INTEGER NOT NULL DEFAULT 1,
                summa REAL NOT NULL DEFAULT 0,
                discount REAL NOT NULL DEFAULT 0,
                bonussum REAL NOT NULL DEFAULT 0,
                summapay REAL NOT NULL DEFAULT 0,
                date TEXT NOT NULL,
                sale_type TEXT NOT NULL DEFAULT 'sale',
                type TEXT NOT NULL DEFAULT '3',
                status TEXT NOT NULL DEFAULT '1',
                sync_status INTEGER NOT NULL DEFAULT 0,
                server_id INTEGER NULL,
                synced_at TEXT NULL,
                sync_error TEXT NULL,
                note TEXT NULL
            );
            """, cancellationToken);

        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS sale_data (
                id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                sale_id INTEGER NOT NULL,
                product_id INTEGER NOT NULL,
                quantity REAL NOT NULL DEFAULT 0,
                price REAL NOT NULL DEFAULT 0,
                discount REAL NOT NULL DEFAULT 0,
                bonus REAL NOT NULL DEFAULT 0,
                FOREIGN KEY (sale_id) REFERENCES sales(id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await context.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS ix_sale_data_sale_id ON sale_data(sale_id);
            """, cancellationToken);
    }

    private async Task EnsureSalesColumnsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (IsMySql)
        {
            await EnsureMySqlColumnAsync(context, "sales", "sale_type", "VARCHAR(30) NOT NULL DEFAULT 'sale'", cancellationToken);
            await EnsureMySqlColumnAsync(context, "sales", "note", "TEXT NULL", cancellationToken);
            return;
        }

        await EnsureSqliteColumnAsync(context, "sales", "sale_type", "TEXT NOT NULL DEFAULT 'sale'", cancellationToken);
        await EnsureSqliteColumnAsync(context, "sales", "note", "TEXT NULL", cancellationToken);
    }

    private static async Task NormalizeSalesCodeValuesAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlRawAsync("""
            UPDATE sales
            SET type = CASE
                WHEN type = '1' OR type IN ('С собой', 'с собой') THEN '1'
                WHEN type = '2' OR type IN ('Доставка', 'доставка') THEN '2'
                WHEN type = '3' OR type IN ('В зале', 'в зале') THEN '3'
                ELSE '3'
            END,
            status = CASE
                WHEN status = '2' OR lower(status) = 'paid' OR status IN ('Оплачен', 'оплачен') THEN '2'
                WHEN status = '1' OR lower(status) = 'open' OR status IN ('Открыт', 'открыт') THEN '1'
                ELSE '1'
            END;
            """, cancellationToken);
    }

    private async Task EnsurePeopleColumnsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (IsMySql)
        {
            var dbConnection = context.Database.GetDbConnection();
            await context.Database.OpenConnectionAsync(cancellationToken);
            try
            {
                await using var command = dbConnection.CreateCommand();
                command.CommandText = """
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'peoples'
                      AND COLUMN_NAME = 'address';
                    """;
                var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
                if (!exists)
                {
                    await context.Database.ExecuteSqlRawAsync("ALTER TABLE peoples ADD COLUMN address TEXT NULL;", cancellationToken);
                }
            }
            finally
            {
                await context.Database.CloseConnectionAsync();
            }
            return;
        }

        if (context.Database.GetDbConnection() is not SqliteConnection connection)
        {
            return;
        }

        var hasAddress = false;
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(peoples);";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var columnName = reader.GetString(1);
                if (string.Equals(columnName, "address", StringComparison.OrdinalIgnoreCase))
                {
                    hasAddress = true;
                    break;
                }
            }
        }
        await context.Database.CloseConnectionAsync();

        if (!hasAddress)
        {
            await context.Database.ExecuteSqlRawAsync("ALTER TABLE peoples ADD COLUMN address TEXT NULL;", cancellationToken);
        }
    }

    private async Task EnsureDdsColumnsAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (IsMySql)
        {
            await EnsureMySqlColumnAsync(context, "dds", "doc_id", "INT NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "dds", "article_id", "INT NOT NULL DEFAULT 1", cancellationToken);
            await EnsureMySqlColumnAsync(context, "dds", "order_type", "VARCHAR(50) NOT NULL DEFAULT 'salepay'", cancellationToken);
            await EnsureMySqlColumnAsync(context, "dds", "sync_status", "INT NOT NULL DEFAULT 0", cancellationToken);
            await EnsureMySqlColumnAsync(context, "dds", "server_id", "INT NULL", cancellationToken);
            await EnsureMySqlColumnAsync(context, "dds", "synced_at", "DATETIME NULL", cancellationToken);
            await EnsureMySqlColumnAsync(context, "dds", "sync_error", "TEXT NULL", cancellationToken);
            return;
        }

        await EnsureSqliteColumnAsync(context, "dds", "doc_id", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "dds", "article_id", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await EnsureSqliteColumnAsync(context, "dds", "order_type", "TEXT NOT NULL DEFAULT 'salepay'", cancellationToken);
        await EnsureSqliteColumnAsync(context, "dds", "sync_status", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "dds", "server_id", "INTEGER NULL", cancellationToken);
        await EnsureSqliteColumnAsync(context, "dds", "synced_at", "TEXT NULL", cancellationToken);
        await EnsureSqliteColumnAsync(context, "dds", "sync_error", "TEXT NULL", cancellationToken);
    }

    private async Task EnsureShiftsTableAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (IsMySql)
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS shifts (
                    id INT NOT NULL PRIMARY KEY,
                    store_id INT NOT NULL DEFAULT 1,
                    cash_id INT NOT NULL DEFAULT 1,
                    opened_by_user_id INT NOT NULL DEFAULT 1,
                    closed_by_user_id INT NULL,
                    opening_balance DECIMAL(18,2) NOT NULL DEFAULT 0,
                    sales_total DECIMAL(18,2) NOT NULL DEFAULT 0,
                    return_total DECIMAL(18,2) NOT NULL DEFAULT 0,
                    sale_payment_total DECIMAL(18,2) NOT NULL DEFAULT 0,
                    payment_income_total DECIMAL(18,2) NOT NULL DEFAULT 0,
                    payment_expense_total DECIMAL(18,2) NOT NULL DEFAULT 0,
                    payment_total DECIMAL(18,2) NOT NULL DEFAULT 0,
                    cash_in_total DECIMAL(18,2) NOT NULL DEFAULT 0,
                    cash_out_total DECIMAL(18,2) NOT NULL DEFAULT 0,
                    closing_balance DECIMAL(18,2) NOT NULL DEFAULT 0,
                    sales_count INT NOT NULL DEFAULT 0,
                    payment_count INT NOT NULL DEFAULT 0,
                    opened_at DATETIME NOT NULL,
                    expires_at DATETIME NOT NULL,
                    closed_at DATETIME NULL,
                    note TEXT NULL,
                    status INT NOT NULL DEFAULT 1
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """, cancellationToken);
            return;
        }

        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS shifts (
                id INTEGER NOT NULL PRIMARY KEY,
                store_id INTEGER NOT NULL DEFAULT 1,
                cash_id INTEGER NOT NULL DEFAULT 1,
                opened_by_user_id INTEGER NOT NULL DEFAULT 1,
                closed_by_user_id INTEGER NULL,
                opening_balance REAL NOT NULL DEFAULT 0,
                sales_total REAL NOT NULL DEFAULT 0,
                return_total REAL NOT NULL DEFAULT 0,
                sale_payment_total REAL NOT NULL DEFAULT 0,
                payment_income_total REAL NOT NULL DEFAULT 0,
                payment_expense_total REAL NOT NULL DEFAULT 0,
                payment_total REAL NOT NULL DEFAULT 0,
                cash_in_total REAL NOT NULL DEFAULT 0,
                cash_out_total REAL NOT NULL DEFAULT 0,
                closing_balance REAL NOT NULL DEFAULT 0,
                sales_count INTEGER NOT NULL DEFAULT 0,
                payment_count INTEGER NOT NULL DEFAULT 0,
                opened_at TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                closed_at TEXT NULL,
                note TEXT NULL,
                status INTEGER NOT NULL DEFAULT 1
            );
            """, cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "store_id", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "cash_id", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "opened_by_user_id", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "closed_by_user_id", "INTEGER NULL", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "opening_balance", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "sales_total", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "return_total", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "sale_payment_total", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "payment_income_total", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "payment_expense_total", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "payment_total", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "cash_in_total", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "cash_out_total", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "closing_balance", "REAL NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "sales_count", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "payment_count", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "opened_at", "TEXT NOT NULL DEFAULT '1970-01-01 00:00:00'", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "expires_at", "TEXT NOT NULL DEFAULT '1970-01-01 00:00:00'", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "closed_at", "TEXT NULL", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "note", "TEXT NULL", cancellationToken);
        await EnsureSqliteColumnAsync(context, "shifts", "status", "INTEGER NOT NULL DEFAULT 1", cancellationToken);
    }

    private async Task EnsureArticlesTableAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (IsMySql)
        {
            await context.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS articles (
                    id INT NOT NULL PRIMARY KEY,
                    parent_id INT NOT NULL DEFAULT 0,
                    name TEXT NULL,
                    status INT NOT NULL DEFAULT 1,
                    `check` INT NOT NULL DEFAULT 0,
                    type VARCHAR(30) NULL
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """, cancellationToken);
            await EnsureMySqlColumnAsync(context, "articles", "type", "VARCHAR(30) NULL", cancellationToken);
            return;
        }

        await context.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS articles (
                id INTEGER NOT NULL PRIMARY KEY,
                parent_id INTEGER NOT NULL DEFAULT 0,
                name TEXT NULL,
                status INTEGER NOT NULL DEFAULT 1,
                "check" INTEGER NOT NULL DEFAULT 0,
                type TEXT NULL
            );
            """, cancellationToken);
        await EnsureSqliteColumnAsync(context, "articles", "type", "TEXT NULL", cancellationToken);
    }

    private static async Task EnsureMySqlColumnAsync(
        AppDbContext context,
        string tableName,
        string columnName,
        string definition,
        CancellationToken cancellationToken)
    {
        var dbConnection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = dbConnection.CreateCommand();
            command.CommandText = $"""
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = '{tableName}'
                  AND COLUMN_NAME = '{columnName}';
                """;
            var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
            if (!exists)
            {
#pragma warning disable EF1002
                await context.Database.ExecuteSqlRawAsync(
                    $"ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {definition};",
                    cancellationToken);
#pragma warning restore EF1002
            }
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static async Task EnsureSqliteColumnAsync(
        AppDbContext context,
        string tableName,
        string columnName,
        string definition,
        CancellationToken cancellationToken)
    {
        if (context.Database.GetDbConnection() is not SqliteConnection connection)
        {
            return;
        }

        var exists = false;
        await context.Database.OpenConnectionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $"PRAGMA table_info({tableName});";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }
        }
        await context.Database.CloseConnectionAsync();

        if (!exists)
        {
#pragma warning disable EF1002
            await context.Database.ExecuteSqlRawAsync(
                $"ALTER TABLE {tableName} ADD COLUMN {columnName} {definition};",
                cancellationToken);
#pragma warning restore EF1002
        }
    }

    private static void SavePaymentRows(
        AppDbContext context,
        Order order,
        IEnumerable<PaymentLine>? payments)
    {
        var lines = (payments ?? Array.Empty<PaymentLine>())
            .Where(payment => payment.Amount > 0)
            .ToList();
        if (lines.Count == 0 && order.SummaPay > 0)
        {
            lines.Add(new PaymentLine(order.CashId, order.SummaPay));
        }

        var existingPayments = context.Dds
            .Where(payment => payment.OrderType == DdsOperationTypes.SalePayment && payment.DocId == order.Number)
            .ToList();
        context.Dds.RemoveRange(existingPayments);

        var nextId = (context.Dds.Select(payment => (int?)payment.Id).Max() ?? 0) + 1;
        var eventTime = new DateTimeOffset(order.Date == default ? DateTime.Now : order.Date).ToUnixTimeSeconds();
        foreach (var payment in lines)
        {
            context.Dds.Add(new Dds
            {
                Id = nextId++,
                DocId = order.Number,
                StoreId = order.StoreId,
                UserId = order.UserId,
                CashId = payment.CashId,
                PeopleId = order.PeopleId,
                ArticleId = 1,
                Summa = payment.Amount,
                EventTime = eventTime,
                OrderType = DdsOperationTypes.SalePayment,
                Description = $"Sale payment #{order.Number}",
                Date = order.Date.ToString("yyyy-MM-dd HH:mm:ss"),
                Status = 1,
                SyncStatus = 0
            });
        }
    }

    private async Task UpsertEntitiesAsync<TEntity>(
        IEnumerable<TEntity> items,
        string keyPropertyName,
        Func<TEntity, int> keySelector,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var list = items.ToList();
        if (list.Count == 0)
        {
            return;
        }

        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            var set = context.Set<TEntity>();
            var keys = list.Select(keySelector).Distinct().ToArray();

            var existingList = await set
                .Where(entity => keys.Contains(EF.Property<int>(entity, keyPropertyName)))
                .ToListAsync(cancellationToken);
            var existing = existingList.ToDictionary(keySelector);

            foreach (var item in list)
            {
                var key = keySelector(item);
                if (existing.TryGetValue(key, out var tracked))
                {
                    context.Entry(tracked).CurrentValues.SetValues(item);
                }
                else
                {
                    set.Add(item);
                }
            }

            await context.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    private static async Task MigrateUsersPasswordToPincodeAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        if (context.Database.GetDbConnection() is not SqliteConnection connection)
        {
            return;
        }

        var hasPassword = false;
        var hasPincode = false;

        await context.Database.OpenConnectionAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA table_info(users);";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var columnName = reader.GetString(1);
                if (string.Equals(columnName, "password", StringComparison.OrdinalIgnoreCase))
                {
                    hasPassword = true;
                }
                else if (string.Equals(columnName, "pincode", StringComparison.OrdinalIgnoreCase))
                {
                    hasPincode = true;
                }
            }
        }
        await context.Database.CloseConnectionAsync();

        if (!hasPassword && !hasPincode)
        {
            await context.Database.ExecuteSqlRawAsync("""
                ALTER TABLE users ADD COLUMN pincode TEXT NOT NULL DEFAULT '';
                """, cancellationToken);
            return;
        }

        if (hasPincode && hasPassword)
        {
            await context.Database.ExecuteSqlRawAsync("""
                UPDATE users
                SET pincode = COALESCE(NULLIF(pincode, ''), password)
                WHERE pincode IS NULL OR pincode = '';
                """, cancellationToken);
            return;
        }

        if (hasPassword && !hasPincode)
        {
            await context.Database.ExecuteSqlRawAsync("""
                ALTER TABLE users RENAME COLUMN password TO pincode;
                """, cancellationToken);
        }
    }
}
