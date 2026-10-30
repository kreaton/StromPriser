using System.Text.Json;

namespace StromPriser;

/// <summary>The user's last selections, kept in %APPDATA%\StromPriser\settings.json between runs.</summary>
public sealed class AppSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StromPriser", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string? SupplierId { get; set; }
    public string? CustomerGroupId { get; set; }
    // Companies have no id in the API, so they are remembered by name.
    public string? CompanyName { get; set; }
    public string? ProductId { get; set; }
    public bool Hourly { get; set; } = true;
    public bool IncludeForecast { get; set; }
    public string? ExportFolder { get; set; }

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), JsonOptions) ?? new();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A missing or corrupt file just means starting from the defaults.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Not remembering the selection is not worth interrupting the user over.
        }
    }
}
