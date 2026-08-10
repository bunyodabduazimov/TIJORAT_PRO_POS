using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FFPOS.Models;

namespace FFPOS.Services;

public class AuthApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient = new();
    private readonly string _baseUrl;

    public AuthApiClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public async Task<AuthApiResponse> SendCodeAsync(string phone, CancellationToken cancellationToken = default)
    {
        return await PostAsync("auth/send-code", new { phone }, cancellationToken);
    }

    public async Task<AuthApiResponse> VerifyCodeAsync(string phone, string code, CancellationToken cancellationToken = default)
    {
        return await PostAsync("auth/verify-code", new { phone, code }, cancellationToken);
    }

    private async Task<AuthApiResponse> PostAsync(string endpoint, object body, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(BuildUrl(endpoint), body, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
            {
                return AuthApiResponse.Error("Пустой ответ сервера");
            }

            var result = JsonSerializer.Deserialize<AuthApiResponse>(json, JsonOptions);
            return result ?? AuthApiResponse.Error("Не удалось прочитать ответ сервера");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return AuthApiResponse.Error("Нет связи с сервером");
        }
    }

    private string BuildUrl(string endpoint)
    {
        return _baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase)
            ? $"{_baseUrl}/{endpoint}"
            : $"{_baseUrl}/api/{endpoint}";
    }
}

public class AuthApiResponse
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; set; }

    public AuthAppDto? App { get; set; }
    public bool IsSuccess => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase);

    public static AuthApiResponse Error(string message)
    {
        return new AuthApiResponse { Status = "error", Message = message };
    }
}

public class AuthAppDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("date_to")]
    public string DateTo { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
    public int Fiscat { get; set; }

    [JsonPropertyName("app_id")]
    public string AppId { get; set; } = string.Empty;

    [JsonPropertyName("public_url")]
    public string PublicUrl { get; set; } = string.Empty;

    public AppInfo ToAppInfo()
    {
        return new AppInfo
        {
            Id = Id,
            Name = Name,
            Phone = Phone,
            DateTo = DateTo,
            Status = Status,
            Fiscat = Fiscat,
            AppId = AppId,
            PublicUrl = PublicUrl
        };
    }
}
