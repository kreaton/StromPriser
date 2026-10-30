using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace StromPriser;

/// <summary>Raised when the API answers with a non-success status; carries the server's message.</summary>
public sealed class StromligningApiException : Exception
{
    public StromligningApiException(string message) : base(message) { }
}

/// <summary>Thin client over the public https://stromligning.dk/api endpoints.</summary>
public sealed class StromligningClient : IDisposable
{
    private const string BaseUrl = "https://stromligning.dk/api";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public StromligningClient()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("StromPriser/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    /// <summary>Grid companies (netselskaber). The chosen one determines the distribution tariff.</summary>
    public Task<List<Supplier>> GetSuppliersAsync(CancellationToken ct) =>
        GetAsync<List<Supplier>>($"{BaseUrl}/suppliers", ct);

    /// <summary>Trading companies (elhandelsselskaber) and their products.</summary>
    public Task<List<Company>> GetCompaniesAsync(CancellationToken ct) =>
        GetAsync<List<Company>>($"{BaseUrl}/companies", ct);

    /// <summary>
    /// Fetches prices for the window [from, to). The API's own default window is
    /// yesterday..tomorrow, so an explicit range is what gets us the coming 24 hours.
    /// </summary>
    public Task<PriceResponse> GetPricesAsync(
        string supplierId,
        string? customerGroupId,
        string? productId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct)
    {
        var query = new List<string>
        {
            "supplierId=" + Uri.EscapeDataString(supplierId),
            "from=" + Uri.EscapeDataString(FormatUtc(from)),
            // The API treats "to" as inclusive of the interval starting at that instant, so we
            // step back a second to land on exactly 24 hours of data rather than one extra slot.
            "to=" + Uri.EscapeDataString(FormatUtc(to.AddSeconds(-1)))
        };

        if (!string.IsNullOrWhiteSpace(customerGroupId))
            query.Add("customerGroupId=" + Uri.EscapeDataString(customerGroupId));

        if (!string.IsNullOrWhiteSpace(productId))
            query.Add("productId=" + Uri.EscapeDataString(productId));

        return GetAsync<PriceResponse>($"{BaseUrl}/prices?{string.Join("&", query)}", ct);
    }

    /// <summary>
    /// The latest hourly spot price forecast for a price area (DK1/DK2), about a week ahead.
    /// Only the spot price is forecast - no tariffs, taxes or surcharge.
    /// </summary>
    public Task<ForecastResponse> GetForecastAsync(string priceArea, CancellationToken ct) =>
        GetAsync<ForecastResponse>(
            $"{BaseUrl}/forecasts/latest?priceArea={Uri.EscapeDataString(priceArea)}", ct);

    private static string FormatUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new StromligningApiException(
                $"{(int)response.StatusCode} {response.ReasonPhrase}: {ExtractError(body)}");

        var result = JsonSerializer.Deserialize<T>(body, JsonOptions);
        return result ?? throw new StromligningApiException("Tomt svar fra API'et.");
    }

    /// <summary>Pulls the "error" field out of an error body, falling back to the raw text.</summary>
    private static string ExtractError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "(intet svar)";

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String)
            {
                return error.GetString() ?? body;
            }
        }
        catch (JsonException)
        {
            // Not JSON - fall through and show whatever came back.
        }

        return body.Length > 300 ? body[..300] + "..." : body;
    }

    public void Dispose() => _http.Dispose();
}
