using System.Text.Json.Serialization;

namespace ElForecast.Services;

/// <summary>The user's selections, kept in the browser's localStorage between visits.</summary>
public sealed class ForecastSetup
{
    public string? SupplierId { get; set; }
    public string? CustomerGroupId { get; set; }
    // Companies have no id in the API, so they are remembered by name.
    public string? CompanyName { get; set; }
    public string? ProductId { get; set; }

    /// <summary>Stack the components incl. VAT (the price actually paid) rather than ex. VAT.</summary>
    public bool IncludeVat { get; set; } = true;

    [JsonIgnore]
    public bool IsComplete => !string.IsNullOrWhiteSpace(SupplierId);

    public ForecastSetup Clone() => (ForecastSetup)MemberwiseClone();
}
