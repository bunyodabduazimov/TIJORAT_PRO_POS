using System.Text.Json;

namespace FFPOS.Models;

public sealed class UserSettings
{
    public int DefaultStockId { get; set; }
    public int DefaultCashId { get; set; }
    public string PrinterNameDefault { get; set; } = string.Empty;
    public bool CheckPrint { get; set; } = true;
    public bool ShowProductImage { get; set; } = true;
    public bool ShowBarcode { get; set; } = true;
    public bool ShowSku { get; set; } = true;
    public bool ShowStockQuantity { get; set; } = true;
    public bool TableCompactMode { get; set; } = true;
    public bool AutoSync { get; set; } = true;
    public bool SyncAfterSale { get; set; }
    public int SyncIntervalMinutes { get; set; } = 60;
    public bool ConfirmBeforeLogout { get; set; } = true;
    public string Note { get; set; } = string.Empty;
    public bool CanChangePrice { get; set; } = true;
    public bool AccessAll { get; set; } = true;
    public List<int> AccessIds { get; set; } = new();
    public int ProductPriceId { get; set; } = 1;
    public int ServicePriceId { get; set; } = 2;
    public bool HasAccess(int accessId) => AccessAll || AccessIds.Contains(accessId);
    public bool CanViewOrders => HasAccess(44);
    public bool CanReturnSale => HasAccess(45);
    public bool CanViewPayments => HasAccess(53);
    public bool CanAddCashInPayment => HasAccess(163);
    public bool CanAddCashOutPayment => HasAccess(164);
    public bool CanAddCustomerOutPayment => HasAccess(165);
    public bool CanAddCustomerInPayment => HasAccess(166);
    public bool CanEditAllPayments => HasAccess(168);
    public bool CanDeleteUnsyncedPayments => HasAccess(170);

    public static UserSettings Parse(string? json, bool? fallbackCheckPrint = null)
    {
        var settings = new UserSettings();
        if (fallbackCheckPrint.HasValue)
        {
            settings.CheckPrint = fallbackCheckPrint.Value;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return settings;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Array)
            {
                var first = root.EnumerateArray().FirstOrDefault();
                return first.ValueKind == JsonValueKind.Object
                    ? ApplyAccessSettings(settings, first)
                    : settings;
            }

            if (root.ValueKind == JsonValueKind.Object && LooksLikeAccessSettings(root))
            {
                return ApplyAccessSettings(settings, root);
            }

            return JsonSerializer.Deserialize<UserSettings>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? settings;
        }
        catch
        {
            return settings;
        }
    }

    private static bool LooksLikeAccessSettings(JsonElement root)
    {
        return root.TryGetProperty("change_price", out _) ||
               root.TryGetProperty("access_all", out _) ||
               root.TryGetProperty("access", out _) ||
               root.TryGetProperty("stock", out _) ||
               root.TryGetProperty("cash", out _) ||
               root.TryGetProperty("prices", out _);
    }

    private static UserSettings ApplyAccessSettings(UserSettings settings, JsonElement root)
    {
        settings.CanChangePrice = ReadBool(root, "change_price", settings.CanChangePrice);
        settings.AccessAll = ReadBool(root, "access_all", settings.AccessAll);
        settings.AccessIds = ReadAccessIds(root);

        if (root.TryGetProperty("stock", out var stock) && stock.ValueKind == JsonValueKind.Object)
        {
            settings.DefaultStockId = ReadInt(stock, "default", settings.DefaultStockId);
        }

        if (root.TryGetProperty("cash", out var cash) && cash.ValueKind == JsonValueKind.Object)
        {
            settings.DefaultCashId = ReadInt(cash, "default", settings.DefaultCashId);
        }

        if (root.TryGetProperty("prices", out var prices) && prices.ValueKind == JsonValueKind.Object)
        {
            settings.ProductPriceId = ReadInt(prices, "product", settings.ProductPriceId);
            settings.ServicePriceId = ReadInt(prices, "service", settings.ServicePriceId);
        }

        return settings;
    }

    private static List<int> ReadAccessIds(JsonElement root)
    {
        var ids = new List<int>();
        if (!root.TryGetProperty("access", out var access) || access.ValueKind != JsonValueKind.Array)
        {
            return ids;
        }

        foreach (var item in access.EnumerateArray())
        {
            var id = item.ValueKind switch
            {
                JsonValueKind.Number when item.TryGetInt32(out var number) => number,
                JsonValueKind.String when int.TryParse(item.GetString(), out var number) => number,
                _ => 0
            };

            if (id > 0 && !ids.Contains(id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    private static int ReadInt(JsonElement root, string key, int fallback)
    {
        if (!root.TryGetProperty(key, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => fallback
        };
    }

    private static bool ReadBool(JsonElement root, string key, bool fallback)
    {
        if (!root.TryGetProperty(key, out var value))
        {
            return fallback;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var number) => number == 1,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var boolean) => boolean,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number == 1,
            _ => fallback
        };
    }
}
