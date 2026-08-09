using System.IO;
using System.Text.Json;
using FFPOS.Models;

namespace FFPOS.Services;

public class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters =
        {
            new FlexibleIntConverter(),
            new FlexibleBoolConverter()
        }
    };

    private readonly string _settingsPath;

    public AppSettingsService()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TIJORAT PRO");

        Directory.CreateDirectory(directory);
        _settingsPath = Path.Combine(directory, "settings.json");
    }

    public AppActivationSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            return Normalize(new AppActivationSettings());
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            using var document = JsonDocument.Parse(json);
            var settings = JsonSerializer.Deserialize<AppActivationSettings>(json, JsonOptions)
                ?? new AppActivationSettings();

            ApplyLegacyKeys(settings, document.RootElement);
            return Normalize(settings);
        }
        catch
        {
            return Normalize(new AppActivationSettings());
        }
    }

    public void Save(AppActivationSettings settings)
    {
        Normalize(settings);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    public bool Exists()
    {
        return File.Exists(_settingsPath);
    }

    public void EnsureCreated()
    {
        if (!File.Exists(_settingsPath))
        {
            Save(new AppActivationSettings());
        }
    }

    public void Reset()
    {
        Save(new AppActivationSettings());
    }

    private static AppActivationSettings Normalize(AppActivationSettings settings)
    {
        settings.BaseUrl = NormalizeBaseUrl(settings.BaseUrl);

        if (settings.App is not null)
        {
            settings.ApplyApp(settings.App);
        }

        if (string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            settings.BaseUrl = "https://app.tijorat.pro";
        }

        if (string.IsNullOrWhiteSpace(settings.PublicUrl))
        {
            settings.PublicUrl = "/public";
        }

        if (string.IsNullOrWhiteSpace(settings.AdminCode))
        {
            settings.AdminCode = "2244";
        }

        if (settings.SyncTimeInmin < 5 || settings.SyncTimeInmin > 1440)
        {
            settings.SyncTimeInmin = 60;
        }

        if (settings.SyncDay < 0 || settings.SyncDay > 30)
        {
            settings.SyncDay = 10;
        }

        if (settings.MaxDiscount < 0 || settings.MaxDiscount > 100)
        {
            settings.MaxDiscount = 10;
        }

        settings.FocusQty = settings.FocusQty == 2 ? 2 : 1;
        settings.AppType = settings.AppType is >= 1 and <= 3 ? settings.AppType : 1;
        settings.DatabaseType = settings.DatabaseType is >= 1 and <= 2 ? settings.DatabaseType : 1;
        settings.MySqlHost = string.IsNullOrWhiteSpace(settings.MySqlHost) ? "127.0.0.1" : settings.MySqlHost.Trim();
        settings.MySqlPort = settings.MySqlPort is >= 1 and <= 65535 ? settings.MySqlPort : 3306;
        settings.PeopleId = settings.PeopleId > 0 ? settings.PeopleId : 1;
        settings.StoreId = settings.StoreId > 0 ? settings.StoreId : 1;
        settings.ProductDefaultId = settings.ProductDefaultId > 0 ? settings.ProductDefaultId : 1;
        settings.IsActivated = settings.IsActivated || !string.IsNullOrWhiteSpace(settings.AppId);

        return settings;
    }

    private static void ApplyLegacyKeys(AppActivationSettings settings, JsonElement root)
    {
        settings.BaseUrl = ReadString(root, "BaseUrl", settings.BaseUrl);
        settings.BaseUrl = ReadString(root, "Url", settings.BaseUrl);
        settings.BaseUrl = ReadString(root, "ApiBaseUrl", settings.BaseUrl);

        settings.AppName = ReadString(root, "AppName", settings.AppName);
        settings.AppName = ReadString(root, "StoreName", settings.AppName);

        settings.AppPhone = ReadString(root, "AppPhone", settings.AppPhone);
        settings.AppPhone = ReadString(root, "StorePhone", settings.AppPhone);

        settings.ProductDefaultId = ReadInt(root, "ProductDefaultId", settings.ProductDefaultId);
        settings.ProductDefaultId = ReadInt(root, "ProductDefailtId", settings.ProductDefaultId);
        settings.PeopleId = ReadInt(root, "PeopleId", settings.PeopleId);
        settings.StoreId = ReadInt(root, "StoreId", settings.StoreId);
        settings.AppType = ReadInt(root, "AppType", settings.AppType);
        settings.DatabaseType = ReadInt(root, "DatabaseType", settings.DatabaseType);
        settings.DatabaseType = ReadInt(root, "DbType", settings.DatabaseType);
        settings.MySqlHost = ReadString(root, "MySqlHost", settings.MySqlHost);
        settings.MySqlPort = ReadInt(root, "MySqlPort", settings.MySqlPort);
        settings.MySqlDatabase = ReadString(root, "MySqlDatabase", settings.MySqlDatabase);
        settings.MySqlUsername = ReadString(root, "MySqlUsername", settings.MySqlUsername);
        settings.MySqlPassword = ReadString(root, "MySqlPassword", settings.MySqlPassword);

        settings.MessageError = ReadBool(root, "MessageError", settings.MessageError);
        settings.MessageError = ReadBool(root, "MessagError", settings.MessageError);

        if (root.TryGetProperty("Pharmacy", out _))
        {
            settings.AppType = ReadInt(root, "Pharmacy", 0) == 1 ? 2 : 1;
        }

        settings.IsActivated = ReadBool(root, "IsActivated", settings.IsActivated);

        if (root.TryGetProperty("App", out var appElement) && appElement.ValueKind == JsonValueKind.Object)
        {
            var app = JsonSerializer.Deserialize<AppInfo>(appElement.GetRawText(), JsonOptions);
            if (app is not null)
            {
                settings.ApplyApp(app);
            }
        }
    }

    private static string ReadString(JsonElement root, string key, string fallback)
    {
        return root.TryGetProperty(key, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? fallback
            : fallback;
    }

    private static int ReadInt(JsonElement root, string key, int fallback)
    {
        if (!root.TryGetProperty(key, out var element))
        {
            return fallback;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var value))
        {
            return value;
        }

        if (element.ValueKind == JsonValueKind.True)
        {
            return 1;
        }

        if (element.ValueKind == JsonValueKind.False)
        {
            return 0;
        }

        return element.ValueKind == JsonValueKind.String && int.TryParse(element.GetString(), out value)
            ? value
            : fallback;
    }

    private static bool ReadBool(JsonElement root, string key, bool fallback)
    {
        if (!root.TryGetProperty(key, out var element))
        {
            return fallback;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt32(out var value) => value == 1,
            JsonValueKind.String when bool.TryParse(element.GetString(), out var value) => value,
            JsonValueKind.String when int.TryParse(element.GetString(), out var value) => value == 1,
            _ => fallback
        };
    }

    private static string NormalizeBaseUrl(string value)
    {
        var trimmed = value.Trim();
        var open = trimmed.IndexOf("](", StringComparison.Ordinal);
        if (trimmed.StartsWith("[", StringComparison.Ordinal) && open > 0 && trimmed.EndsWith(")", StringComparison.Ordinal))
        {
            return trimmed[(open + 2)..^1].Trim();
        }

        return trimmed;
    }
}
