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

        var articles = await PostArrayAsync<Article>("articles", appId, null, cancellationToken);
        await _databaseService.UpsertArticlesAsync(articles, cancellationToken);
        AddResult(result, "Articles", articles.Count);

        var categories = await PostArrayAsync<Category>("categories", appId, null, cancellationToken);
        await _databaseService.UpsertCategoriesAsync(categories, cancellationToken);
        AddResult(result, "Категории", categories.Count);
        progress?.Report(new SyncProgress(76, "Категории", $"Получено: {categories.Count}"));

        var productRequest = BuildProductRequest(users, prices);
        var products = await PostArrayAsync<Product>("products", appId, productRequest, cancellationToken);
        await _databaseService.UpsertProductsAsync(products, cancellationToken);
        AddResult(result, "Товары", products.Count);
        progress?.Report(new SyncProgress(100, "Готово", $"Синхронизация завершена. Товаров: {products.Count}"));

        var uploadedSales = await SyncPendingSalesAsync(cancellationToken);
        AddResult(result, "Uploaded sales", uploadedSales);

        return string.Join(Environment.NewLine, result);
    }

    public async Task<int> SyncPendingSalesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.EffectiveApiBaseUrl) ||
            string.IsNullOrWhiteSpace(_settings.EffectiveAppId))
        {
            return 0;
        }

        await _databaseService.InitializeAsync(cancellationToken);

        var orders = await _databaseService.GetPendingSalesForSyncAsync(cancellationToken);
        if (orders.Count == 0)
        {
            return 0;
        }

        var payments = await _databaseService.GetPaymentsForSalesAsync(
            orders.Select(order => order.Number),
            cancellationToken);
        var paymentsByOrder = payments
            .GroupBy(payment => payment.DocId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var uploaded = 0;
        foreach (var order in orders)
        {
            try
            {
                paymentsByOrder.TryGetValue(order.Number, out var orderPayments);
                var response = await PostSaleAsync(order, orderPayments ?? (IReadOnlyList<Dds>)Array.Empty<Dds>(), cancellationToken);
                await _databaseService.MarkSaleSyncSucceededAsync(order.Number, response.ServerId, response.PaymentServerIds, cancellationToken);
                uploaded++;
            }
            catch (Exception ex)
            {
                await _databaseService.MarkSaleSyncFailedAsync(order.Number, ex.Message, cancellationToken);
            }
        }

        return uploaded;
    }

    public async Task<People> CreatePeopleAsync(People people, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.EffectiveApiBaseUrl))
        {
            throw new InvalidOperationException("URL сервера не настроен.");
        }

        if (string.IsNullOrWhiteSpace(_settings.EffectiveAppId))
        {
            throw new InvalidOperationException("AppId не найден.");
        }

        var body = new
        {
            app_id = _settings.EffectiveAppId,
            name = people.Name,
            phone = people.Phone,
            address = people.Address,
            balance = people.Balance,
            status = people.Status == 0 ? 1 : people.Status
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, GetPosUrl("peoples/create"));
        request.Headers.TryAddWithoutValidation("AppId", _settings.EffectiveAppId);
        request.Headers.Accept.ParseAdd("application/json");
        request.Content = JsonContent.Create(body);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(string.IsNullOrWhiteSpace(json)
                ? $"POST {new Uri(request.RequestUri!.ToString()).PathAndQuery}: {(int)response.StatusCode} {response.ReasonPhrase}"
                : $"POST {new Uri(request.RequestUri!.ToString()).PathAndQuery}: {json}");
        }

        var created = ParsePeopleResponse(json, people);
        if (created.Id <= 0)
        {
            throw new InvalidOperationException("Сервер создал контрагента, но не вернул id.");
        }

        return created;
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

    private async Task<SaleSyncResponse> PostSaleAsync(
        Order order,
        IReadOnlyList<Dds> payments,
        CancellationToken cancellationToken)
    {
        var routes = GetCandidateUrls("sales/sync")
            .Concat(GetCandidateUrls("sales/upload"))
            .Concat(GetCandidateUrls("sales"))
            .Distinct()
            .ToList();
        var body = BuildSalePayload(order, payments);
        HttpResponseMessage? lastResponse = null;
        string? lastJson = null;

        foreach (var route in routes)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, route);
            request.Headers.TryAddWithoutValidation("AppId", _settings.EffectiveAppId);
            request.Headers.Accept.ParseAdd("application/json");
            request.Content = JsonContent.Create(body);

            lastResponse?.Dispose();
            lastResponse = await _httpClient.SendAsync(request, cancellationToken);
            lastJson = await lastResponse.Content.ReadAsStringAsync(cancellationToken);

            if (lastResponse.IsSuccessStatusCode)
            {
                return ParseSaleSyncResponse(lastJson);
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

    private string GetPosUrl(string endpoint)
    {
        var baseUrl = _settings.EffectiveApiBaseUrl.TrimEnd('/');
        return baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
            ? $"{baseUrl}/pos/{endpoint}"
            : $"{baseUrl}/api/pos/{endpoint}";
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

    private object BuildSalePayload(Order order, IReadOnlyList<Dds> payments)
    {
        return new
        {
            app_id = _settings.EffectiveAppId,
            sale = new
            {
                id = order.Number,
                store_id = order.StoreId,
                stock_id = order.StockId,
                user_id = order.UserId,
                cash_id = order.CashId,
                price_id = order.PriceId,
                people_id = order.PeopleId,
                summa = order.Subtotal,
                discount = order.Discount,
                bonussum = order.BonusSum,
                summapay = order.SummaPay,
                note = order.Note,
                date = order.Date.ToString("yyyy-MM-dd HH:mm:ss"),
                sale_type = order.SaleType,
                type = OrderCodes.ToOrderTypeCode(order.OrderType),
                status = OrderCodes.ToStatusCode(order.Status)
            },
            sale_data = order.Items.Select(item => new
            {
                id = item.Id,
                sale_id = order.Number,
                product_id = item.ProductId > 0 ? item.ProductId : item.Product?.Id ?? 0,
                quantity = item.Quantity,
                price = item.Price,
                discount = item.Discount,
                bonus = item.Bonus
            }).ToList(),
            dds = payments.Select(payment => new
            {
                id = payment.Id,
                doc_id = payment.DocId,
                store_id = payment.StoreId,
                user_id = payment.UserId,
                cash_id = payment.CashId,
                people_id = payment.PeopleId,
                article_id = payment.ArticleId,
                summa = payment.Summa,
                event_time = payment.EventTime,
                order_type = payment.OrderType,
                description = payment.Description,
                date = payment.Date,
                status = payment.Status,
                server_id = payment.ServerId
            }).ToList()
        };
    }

    private static People ParsePeopleResponse(string json, People fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            foreach (var element in EnumeratePossiblePeopleElements(root))
            {
                if (TryParsePeopleElement(element, fallback, out var parsed))
                {
                    return parsed;
                }
            }

            if (TryReadServerId(root, out var id))
            {
                fallback.Id = id;
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }

    private static bool TryParsePeopleElement(JsonElement element, People fallback, out People people)
    {
        people = new People
        {
            Name = fallback.Name,
            Phone = fallback.Phone,
            Address = fallback.Address,
            Balance = fallback.Balance,
            Status = fallback.Status == 0 ? 1 : fallback.Status
        };

        if (element.ValueKind != JsonValueKind.Object ||
            !TryReadServerId(element, out var id))
        {
            return false;
        }

        people.Id = id;
        people.Name = ReadString(element, "name") ?? people.Name;
        people.Phone = ReadString(element, "phone") ?? people.Phone;
        people.Address = ReadString(element, "address") ?? people.Address;
        people.Balance = ReadDecimal(element, "balance") ?? people.Balance;
        people.Status = ReadInt(element, "status") ?? people.Status;
        return true;
    }

    private static IEnumerable<JsonElement> EnumeratePossiblePeopleElements(JsonElement root)
    {
        yield return root;

        if (root.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var propertyName in new[] { "data", "result", "people", "peoples", "client", "customer", "item" })
        {
            if (root.TryGetProperty(propertyName, out var property))
            {
                yield return property;
                if (property.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var nestedName in new[] { "data", "people", "client", "customer", "item" })
                {
                    if (property.TryGetProperty(nestedName, out var nested))
                    {
                        yield return nested;
                    }
                }
            }
        }
    }

    private static SaleSyncResponse ParseSaleSyncResponse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SaleSyncResponse(null, new Dictionary<int, int>());
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (TryReadServerId(root, out var serverId))
            {
                return new SaleSyncResponse(serverId, ParsePaymentServerIds(root));
            }

            foreach (var propertyName in new[] { "data", "result", "sale" })
            {
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty(propertyName, out var nested) &&
                    TryReadServerId(nested, out serverId))
                {
                    return new SaleSyncResponse(serverId, ParsePaymentServerIds(root));
                }
            }
        }
        catch (JsonException)
        {
        }

        return new SaleSyncResponse(null, ParsePaymentServerIdsFromJson(json));
    }

    private static IReadOnlyDictionary<int, int> ParsePaymentServerIdsFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<int, int>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ParsePaymentServerIds(document.RootElement);
        }
        catch (JsonException)
        {
            return new Dictionary<int, int>();
        }
    }

    private static IReadOnlyDictionary<int, int> ParsePaymentServerIds(JsonElement root)
    {
        var ids = new Dictionary<int, int>();
        foreach (var arrayName in new[] { "dds", "payments" })
        {
            if (TryReadPaymentServerIds(root, arrayName, ids))
            {
                return ids;
            }

            foreach (var objectName in new[] { "data", "result", "sale" })
            {
                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty(objectName, out var nested) &&
                    TryReadPaymentServerIds(nested, arrayName, ids))
                {
                    return ids;
                }
            }
        }

        return ids;
    }

    private static bool TryReadPaymentServerIds(JsonElement element, string arrayName, Dictionary<int, int> ids)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(arrayName, out var array) ||
            array.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object &&
                TryReadIntProperty(item, "id", out var localId) &&
                TryReadServerId(item, out var serverId))
            {
                ids[localId] = serverId;
            }
        }

        return true;
    }

    private static bool TryReadIntProperty(JsonElement element, string name, out int value)
    {
        value = 0;
        if (!element.TryGetProperty(name, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
        {
            return value > 0;
        }

        return property.ValueKind == JsonValueKind.String &&
               int.TryParse(property.GetString(), out value) &&
               value > 0;
    }

    private static bool TryReadServerId(JsonElement element, out int serverId)
    {
        serverId = 0;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var name in new[] { "server_id", "id", "doc_id", "people_id", "client_id", "customer_id" })
        {
            if (!element.TryGetProperty(name, out var property))
            {
                continue;
            }

            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt32(out serverId))
            {
                return serverId > 0;
            }

            if (property.ValueKind == JsonValueKind.String &&
                int.TryParse(property.GetString(), out serverId))
            {
                return serverId > 0;
            }
        }

        return false;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
        {
            return value;
        }

        return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out value)
            ? value
            : null;
    }

    private static decimal? ReadDecimal(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
        {
            return value;
        }

        return property.ValueKind == JsonValueKind.String &&
               decimal.TryParse(
                   property.GetString()?.Replace(',', '.'),
                   System.Globalization.NumberStyles.Number,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out value)
            ? value
            : null;
    }

    private static void AddResult(List<string> result, string title, int count)
    {
        result.Add($"{title}: {count}");
    }
}

internal sealed record SaleSyncResponse(int? ServerId, IReadOnlyDictionary<int, int> PaymentServerIds);
