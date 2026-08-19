using FFPOS.Models;
using Microsoft.EntityFrameworkCore;

namespace FFPOS.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Stock> Stoks => Set<Stock>();
    public DbSet<Cash> Cashes => Set<Cash>();
    public DbSet<Price> Prices => Set<Price>();
    public DbSet<PriceData> PriceData => Set<PriceData>();
    public DbSet<User> Users => Set<User>();
    public DbSet<People> Peoples => Set<People>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<Dds> Dds => Set<Dds>();
    public DbSet<Shift> Shifts => Set<Shift>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureStores(modelBuilder);
        ConfigureCategories(modelBuilder);
        ConfigureProducts(modelBuilder);
        ConfigureOrders(modelBuilder);
        ConfigureOrderItems(modelBuilder);
        ConfigureStocks(modelBuilder);
        ConfigureCashes(modelBuilder);
        ConfigurePrices(modelBuilder);
        ConfigurePriceData(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigurePeoples(modelBuilder);
        ConfigureArticles(modelBuilder);
        ConfigureDds(modelBuilder);
        ConfigureShifts(modelBuilder);
    }

    private static void ConfigureStores(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Store>();
        entity.ToTable("stores");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.Name).HasColumnName("name");
        entity.Property(x => x.AgelName).HasColumnName("agel_name");
        entity.Property(x => x.Location).HasColumnName("location");
        entity.Property(x => x.Phone).HasColumnName("phone");
        entity.Property(x => x.Email).HasColumnName("email");
        entity.Property(x => x.Site).HasColumnName("site");
        entity.Property(x => x.Description).HasColumnName("description");
        entity.Property(x => x.Settings).HasColumnName("settings");
        entity.Property(x => x.Status).HasColumnName("status");
    }

    private static void ConfigureCategories(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Category>();
        entity.ToTable("categories");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.ParentId).HasColumnName("parent_id");
        entity.Property(x => x.Name).HasColumnName("name");
        entity.Property(x => x.Image).HasColumnName("image");
        entity.Property(x => x.IconPath).HasColumnName("icon_path");
        entity.Property(x => x.SortOrder).HasColumnName("sort_order");
        entity.Property(x => x.Status).HasColumnName("status");
        entity.Property(x => x.IsActiveValue).HasColumnName("is_active");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Ignore(x => x.IsSelected);
        entity.Ignore(x => x.IconGlyph);
    }

    private static void ConfigureProducts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Product>();
        entity.ToTable("products");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.CategoryId).HasColumnName("category_id");
        entity.Property(x => x.Name).HasColumnName("name");
        entity.Property(x => x.Price).HasColumnName("price");
        entity.Property(x => x.Image).HasColumnName("image");
        entity.Property(x => x.ImagePath).HasColumnName("image_path");
        entity.Property(x => x.Sku).HasColumnName("sku");
        entity.Property(x => x.Barcode).HasColumnName("barcode");
        entity.Property(x => x.PosView).HasColumnName("pos_view");
        entity.Property(x => x.Status).HasColumnName("status");
        entity.Property(x => x.UnitId).HasColumnName("unit_id");
        entity.Property(x => x.Unit).HasColumnName("unit");
        entity.Property(x => x.Category).HasColumnName("category");
        entity.Property(x => x.Quantity).HasColumnName("quantity");
        entity.Property(x => x.Package).HasColumnName("package");
        entity.Property(x => x.SortOrder).HasColumnName("sort_order");
        entity.Property(x => x.IsActive).HasColumnName("is_active");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity.Ignore(x => x.SelectedQuantity);
        entity.Ignore(x => x.HasSelectedQuantity);
    }

    private static void ConfigureOrders(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Order>();
        entity.ToTable("sales");
        entity.HasKey(x => x.Number);
        entity.Property(x => x.Number).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.StoreId).HasColumnName("store_id");
        entity.Property(x => x.StockId).HasColumnName("stock_id");
        entity.Property(x => x.UserId).HasColumnName("user_id");
        entity.Property(x => x.CashId).HasColumnName("cash_id");
        entity.Property(x => x.PriceId).HasColumnName("price_id");
        entity.Property(x => x.PeopleId).HasColumnName("people_id");
        entity.Property(x => x.Summa).HasColumnName("summa");
        entity.Property(x => x.Discount).HasColumnName("discount");
        entity.Property(x => x.BonusSum).HasColumnName("bonussum");
        entity.Property(x => x.SummaPay).HasColumnName("summapay");
        entity.Property(x => x.Note).HasColumnName("note");
        entity.Property(x => x.Date).HasColumnName("date");
        entity.Property(x => x.SaleType).HasColumnName("sale_type");
        entity.Property(x => x.OrderType)
            .HasColumnName("type")
            .HasConversion(
                value => OrderCodes.ToOrderTypeCode(value),
                value => OrderCodes.ToOrderTypeName(value));
        entity.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion(
                value => OrderCodes.ToStatusCode(value),
                value => OrderCodes.ToStatusName(value));
        entity.Property(x => x.SyncStatus).HasColumnName("sync_status");
        entity.Property(x => x.ServerId).HasColumnName("server_id");
        entity.Property(x => x.SyncedAt).HasColumnName("synced_at");
        entity.Property(x => x.SyncError).HasColumnName("sync_error");
        entity.Ignore(x => x.Subtotal);
        entity.Ignore(x => x.Total);
        entity.Ignore(x => x.DisplayName);
        entity.Ignore(x => x.SaleTypeText);
        entity.Ignore(x => x.IsSelected);

        entity.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.OrderNumber)
            .HasPrincipalKey(x => x.Number)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureOrderItems(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<OrderItem>();
        entity.ToTable("sale_data");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        entity.Property(x => x.OrderNumber).HasColumnName("sale_id");
        entity.Property(x => x.ProductId).HasColumnName("product_id");
        entity.Property(x => x.Quantity).HasColumnName("quantity");
        entity.Property(x => x.Price).HasColumnName("price");
        entity.Property(x => x.Discount).HasColumnName("discount");
        entity.Property(x => x.Bonus).HasColumnName("bonus");
        entity.Ignore(x => x.Total);
    }

    private static void ConfigureStocks(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Stock>();
        entity.ToTable("stoks");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.Name).HasColumnName("name");
        entity.Property(x => x.Status).HasColumnName("status");
    }

    private static void ConfigureCashes(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Cash>();
        entity.ToTable("cashes");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.Name).HasColumnName("name");
        entity.Property(x => x.Status).HasColumnName("status");
    }

    private static void ConfigurePrices(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Price>();
        entity.ToTable("prices");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.Name).HasColumnName("name");
        entity.Property(x => x.Status).HasColumnName("status");
        entity.HasMany(x => x.PriceData)
            .WithOne()
            .HasForeignKey(x => x.PriceId)
            .HasPrincipalKey(x => x.Id)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigurePriceData(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<PriceData>();
        entity.ToTable("price_data");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.PriceId).HasColumnName("price_id");
        entity.Property(x => x.ProductId).HasColumnName("product_id");
        entity.Property(x => x.Price).HasColumnName("price");
        entity.Property(x => x.Bonus).HasColumnName("bonus");
        entity.Property(x => x.Discount).HasColumnName("discount");
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<User>();
        entity.ToTable("users");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.StoreId).HasColumnName("store_id");
        entity.Property(x => x.CashId).HasColumnName("cash_id");
        entity.Property(x => x.StockId).HasColumnName("stock_id");
        entity.Property(x => x.Name).HasColumnName("name");
        entity.Property(x => x.Username).HasColumnName("username");
        entity.Property(x => x.Pincode).HasColumnName("pincode");
        entity.Property(x => x.Settings).HasColumnName("settings");
        entity.Property(x => x.Status).HasColumnName("status");
    }

    private static void ConfigurePeoples(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<People>();
        entity.ToTable("peoples");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.Name).HasColumnName("name");
        entity.Property(x => x.Phone).HasColumnName("phone");
        entity.Property(x => x.Address).HasColumnName("address");
        entity.Property(x => x.Balance).HasColumnName("balance");
        entity.Property(x => x.Status).HasColumnName("status");
    }

    private static void ConfigureArticles(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Article>();
        entity.ToTable("articles");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.ParentId).HasColumnName("parent_id");
        entity.Property(x => x.Name).HasColumnName("name");
        entity.Property(x => x.Status).HasColumnName("status");
        entity.Property(x => x.Check).HasColumnName("check");
        entity.Property(x => x.Type).HasColumnName("type");
    }

    private static void ConfigureDds(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Dds>();
        entity.ToTable("dds");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.DocId).HasColumnName("doc_id");
        entity.Property(x => x.StoreId).HasColumnName("store_id");
        entity.Property(x => x.UserId).HasColumnName("user_id");
        entity.Property(x => x.CashId).HasColumnName("cash_id");
        entity.Property(x => x.PeopleId).HasColumnName("people_id");
        entity.Property(x => x.ArticleId).HasColumnName("article_id");
        entity.Property(x => x.Summa).HasColumnName("summa");
        entity.Property(x => x.EventTime).HasColumnName("event_time");
        entity.Property(x => x.OrderType).HasColumnName("order_type");
        entity.Property(x => x.Description).HasColumnName("description");
        entity.Property(x => x.Date).HasColumnName("date");
        entity.Property(x => x.Status).HasColumnName("status");
        entity.Property(x => x.SyncStatus).HasColumnName("sync_status");
        entity.Property(x => x.ServerId).HasColumnName("server_id");
        entity.Property(x => x.SyncedAt).HasColumnName("synced_at");
        entity.Property(x => x.SyncError).HasColumnName("sync_error");
    }

    private static void ConfigureShifts(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<Shift>();
        entity.ToTable("shifts");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Id).ValueGeneratedNever();
        entity.Property(x => x.StoreId).HasColumnName("store_id");
        entity.Property(x => x.CashId).HasColumnName("cash_id");
        entity.Property(x => x.OpenedByUserId).HasColumnName("opened_by_user_id");
        entity.Property(x => x.ClosedByUserId).HasColumnName("closed_by_user_id");
        entity.Property(x => x.OpeningBalance).HasColumnName("opening_balance");
        entity.Property(x => x.SalesTotal).HasColumnName("sales_total");
        entity.Property(x => x.ReturnTotal).HasColumnName("return_total");
        entity.Property(x => x.SalePaymentTotal).HasColumnName("sale_payment_total");
        entity.Property(x => x.PaymentIncomeTotal).HasColumnName("payment_income_total");
        entity.Property(x => x.PaymentExpenseTotal).HasColumnName("payment_expense_total");
        entity.Property(x => x.PaymentTotal).HasColumnName("payment_total");
        entity.Property(x => x.CashInTotal).HasColumnName("cash_in_total");
        entity.Property(x => x.CashOutTotal).HasColumnName("cash_out_total");
        entity.Property(x => x.ClosingBalance).HasColumnName("closing_balance");
        entity.Property(x => x.SalesCount).HasColumnName("sales_count");
        entity.Property(x => x.PaymentCount).HasColumnName("payment_count");
        entity.Property(x => x.OpenedAt).HasColumnName("opened_at");
        entity.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        entity.Property(x => x.ClosedAt).HasColumnName("closed_at");
        entity.Property(x => x.Note).HasColumnName("note");
        entity.Property(x => x.Status).HasColumnName("status");
    }
}
