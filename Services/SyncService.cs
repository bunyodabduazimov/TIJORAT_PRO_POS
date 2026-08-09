using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FFPOS.Models;

namespace FFPOS.Services;

public record SyncProgress(int Percent, string Title, string Message, bool IsError = false);

public class SyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppActivationSettings _settings;
    private readonly DatabaseService _databaseService = new();
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(45)
    };

    public SyncService(AppActivationSettings settings)
    {
        _settings = settings;
    }

    public async Task<string> SyncAsync(IProgress<SyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.EffectiveApiBaseUrl))
        {
            throw new InvalidOperationException("URL сервера не настроен.");
        }

        if (string.IsNullOrWhiteSpace(_settings.EffectiveAppId))
        {
            throw new InvalidOperationException("AppId не найден. Сначала активируйте программу.");
        }

        await _databaseService.InitializeAsync(cancellationToken);

        var result = new List<string>();
        var appId = _settings.EffectiveAppId;

        progress?.Report(new SyncProgress(5, "Подключение", $"Сервер: {_settings.EffectiveApiBaseUrl}"));

        var stores = await PostArrayAsync<Store>("stores", appId, null, cancellationToken);
        await _databaseService.UpsertStoresAsync(stores, cancellationToken);
        AddResult(result, "Магазины", stores.Count);
        progress?.Report(new SyncProgress(15, "Магазины", $"Получено: {stores.Count}"));

        var stoks = await PostArrayAsync<Stock>("stoks", appId, null, cancellationToken);
        await _databaseService.UpsertStoksAsync(stoks, cancellationToken);
        AddResult(result, "Склады", stoks.Count);
        progress?.Report(new SyncProgress(27, "Склады", $"Получено: {stoks.Count}"));

        var cashes = await PostArrayAsync<Cash>("cashes", appId, null, cancellationToken);
        await _databaseService.UpsertCashesAsync(cashes, cancellationToken);
        AddResult(result, "Кассы", cashes.Count);
        progress?.Report(new SyncProgress(34, "Кассы", $"Получено: {cashes.Count}"));

        var prices = await PostArrayAsync<Price>("prices", appId, null, cancellationToken);
        await _databaseService.UpsertPricesAsync(prices, cancellationToken);
        var priceData = prices.SelectMany(price => price.PriceData).ToList();
        if (priceData.Count > 0)
        {
            await _databaseService.UpsertPriceDataAsync(priceData, cancellationToken);
        }

        AddResult(result, "Цены", prices.Count);
        if (priceData.Count > 0)
        {
            AddResult(result, "Данные цен", priceData.Count);
        }

        progress?.Report(new SyncProgress(43, "Цены", $"Получено: {prices.Count}, данные цен: {priceData.Count}"));

        var users = await PostArrayAsync<User>("users", appId, null, cancellationToken);
        await _databaseService.UpsertUsersAsync(users, cancellationToken);
        AddResult(result, "Пользователи", users.Count);
        progress?.Report(new SyncProgress(54, "Пользователи", $"Получено: {users.Count}"));

        var peoples = await PostArrayAsync<People>("peoples", appId, null, cancellationToken);
        await _databaseService.UpsertPeoplesAsync(peoples, cancellationToken);
        AddResult(result, "Контрагенты", peoples.Count);
        progress?.Report(new SyncProgress(66, "Контрагенты", $"Получено: {peoples.Count}"));

        var categories = await PostArrayAsync<Category>("categories", appId, null, cancellationToken);
        await _databaseService.UpsertCategoriesAsync(categories, cancellationToken);
        AddResult(result, "Категории", categories.Count);
        progress?.Report(new SyncProgress(76, "Категории", $"Получено: {categories.Count}"));

        var productRequest = BuildProductRequest(users, prices);
        var products = await PostArrayAsync<Product>("products", appId, productRequest, cancellationToken);
        await _databaseService.UpsertProductsAsync(products, cancellationToken);
        AddResult(result, "Товары", products.Count);
        progress?.Report(new SyncProgress(100, "Готово", $"Синхронизация завершена. Товаров: {products.Count}"));

        return string.Join(Environment.NewLine, result);
    }

    private async Task<List<T>> PostArrayAsync<T>(
        string endpoint,
        string appId,
        object? body,
        CancellationToken cancellationToken)
    {
        var routes = GetCandidateUrls(endpoint);
        HttpResponseMessage? lastResponse = null;
        string? lastJson = null;

        foreach (var route in routes)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, route);
            request.Headers.TryAddWithoutValidation("AppId", appId);
            request.Headers.Accept.ParseAdd("application/json");
            request.Content = JsonContent.Create(body ?? new { });

            lastResponse?.Dispose();
            lastResponse = await _httpClient.SendAsync(request, cancellationToken);
            lastJson = await lastResponse.Content.ReadAsStringAsync(cancellationToken);

            if (lastResponse.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(lastJson))
                {
                    return new List<T>();
                }

                try
                {
                    return DeserializeArrayPayload<T>(lastJson);
                }
                catch (JsonException ex)
                {
                    throw new HttpRequestException($"POST {route}: ответ API не является массивом ожидаемого типа. {ex.Message}\n{lastJson}", ex);
                }
            }

            if (!LooksLikeMissingRoute(lastJson))
            {
                break;
            }
        }

        var endpointInfo = string.Join(" | ", routes.Select(route => new Uri(route).PathAndQuery));
        throw new HttpRequestException(string.IsNullOrWhiteSpace(lastJson)
            ? $"POST {endpointInfo}: {(int?)lastResponse?.StatusCode} {lastResponse?.ReasonPhrase}"
            : $"POST {endpointInfo}: {lastJson}");
    }

    private IEnumerable<string> GetCandidateUrls(string endpoint)
    {
        var baseUrl = _settings.EffectiveApiBaseUrl.TrimEnd('/');
        if (baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{baseUrl}/pos/{endpoint}";
            yield return $"{baseUrl.TrimEnd('/')}/pos/{endpoint}";
            yield break;
        }

        yield return $"{baseUrl}/api/pos/{endpoint}";
        yield return $"{baseUrl}/pos/{endpoint}";
    }

    private static bool LooksLikeMissingRoute(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        return responseText.Contains("could not be found", StringComparison.OrdinalIgnoreCase)
               || responseText.Contains("route", StringComparison.OrdinalIgnoreCase);
    }

    private static List<T> DeserializeArrayPayload<T>(string json)
    {
        var trimmed = json.Trim();
        if (trimmed.Length == 0)
        {
            return new List<T>();
        }

        if (trimmed[0] == '[')
        {
            return JsonSerializer.Deserialize<List<T>>(trimmed, JsonOptions) ?? new List<T>();
        }

        using var document = JsonDocument.Parse(trimmed);
        if (document.RootElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "data", "items", "result", "results", "prices", "users", "stores", "stoks", "cashes", "peoples", "categories", "products" })
            {
                if (document.RootElement.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<T>>(property.GetRawText(), JsonOptions) ?? new List<T>();
                }
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    return JsonSerializer.Deserialize<List<T>>(property.Value.GetRawText(), JsonOptions) ?? new List<T>();
                }
            }
        }

        throw new JsonException($"Ожидался массив JSON, но получен формат: {document.RootElement.ValueKind}");
    }

    private object BuildProductRequest(IReadOnlyList<User> users, IReadOnlyList<Price> prices)
    {
        var user = users.FirstOrDefault();
        var price = prices.FirstOrDefault();
        return new
        {
            user_id = user?.Id ?? 1,
            price_id = _settings.PriceId > 0 ? _settings.PriceId : price?.Id ?? 1,
            stock_id = _settings.StockId > 0 ? _settings.StockId : user?.StockId ?? 1,
            day = Math.Max(0, _settings.SyncDay)
        };
    }

    private static void AddResult(List<string> result, string title, int count)
    {
        result.Add($"{title}: {count}");
    }
}
