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
            trackedOrder.SummaPay = order.Total;
            trackedOrder.Date = order.Date == default ? DateTime.Now : order.Date;
            trackedOrder.SyncStatus = 0;
            trackedOrder.SyncError = null;

            if (existing is not null)
            {
                trackedOrder.Items.Clear();
            }

            foreach (var sourceItem in order.Items)
            {
                if (sourceItem.Product is null)
                {
                    continue;
                }

                if (context.Entry(sourceItem.Product).State == EntityState.Detached)
                {
                    context.Attach(sourceItem.Product);
                }

                trackedOrder.Items.Add(new OrderItem
                {
                    ProductId = sourceItem.Product.Id,
                    Product = sourceItem.Product,
                    Quantity = sourceItem.Quantity,
                    Note = sourceItem.Note,
                    Price = sourceItem.Price > 0 ? sourceItem.Price : sourceItem.Product.Price,
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

    public async Task MarkOrderPaidAsync(Order order, CancellationToken cancellationToken = default)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            await using var context = CreateContext();
            var tracked = await context.Orders.FirstOrDefaultAsync(existing => existing.Number == order.Number, cancellationToken);
            if (tracked is null)
            {
                return;
            }

            tracked.Status = "paid";
            tracked.Summa = order.Subtotal;
            tracked.SummaPay = order.Total;
            tracked.Discount = order.Discount;
            tracked.SyncStatus = 0;
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
                    type VARCHAR(50) NOT NULL DEFAULT 'open',
                    status VARCHAR(30) NOT NULL DEFAULT 'open',
                    sync_status INT NOT NULL DEFAULT 0,
                    server_id INT NULL,
                    synced_at DATETIME NULL,
                    sync_error TEXT NULL
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
                    note TEXT NULL,
                    INDEX ix_sale_data_sale_id (sale_id),
                    CONSTRAINT fk_sale_data_sales_sale_id FOREIGN KEY (sale_id) REFERENCES sales(id) ON DELETE CASCADE
                ) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
                """, cancellationToken);
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
                type TEXT NOT NULL DEFAULT 'open',
                status TEXT NOT NULL DEFAULT 'open',
                sync_status INTEGER NOT NULL DEFAULT 0,
                server_id INTEGER NULL,
                synced_at TEXT NULL,
                sync_error TEXT NULL
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
                note TEXT NULL,
                FOREIGN KEY (sale_id) REFERENCES sales(id) ON DELETE CASCADE
            );
            """, cancellationToken);

        await context.Database.ExecuteSqlRawAsync("""
            CREATE INDEX IF NOT EXISTS ix_sale_data_sale_id ON sale_data(sale_id);
            """, cancellationToken);
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
