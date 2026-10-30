using System.Text.Json;
using Microsoft.JSInterop;

namespace ElForecast.Services;

/// <summary>Loads and saves <see cref="ForecastSetup"/> in localStorage.</summary>
public sealed class SetupStore
{
    private const string Key = "elforecast.setup";

    private readonly IJSRuntime _js;
    private ForecastSetup? _cached;

    public SetupStore(IJSRuntime js) => _js = js;

    public async Task<ForecastSetup> LoadAsync()
    {
        if (_cached is not null)
            return _cached.Clone();

        try
        {
            var json = await _js.InvokeAsync<string?>("elForecast.storageGet", Key);
            if (!string.IsNullOrWhiteSpace(json))
                _cached = JsonSerializer.Deserialize<ForecastSetup>(json);
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            // Blocked or corrupt storage just means starting from the defaults.
        }

        _cached ??= new ForecastSetup();
        return _cached.Clone();
    }

    public async Task SaveAsync(ForecastSetup setup)
    {
        _cached = setup.Clone();

        try
        {
            await _js.InvokeVoidAsync("elForecast.storageSet", Key, JsonSerializer.Serialize(setup));
        }
        catch (JSException)
        {
            // Not remembering the selection is not worth interrupting the user over; it still
            // holds for this visit through the cache.
        }
    }
}
