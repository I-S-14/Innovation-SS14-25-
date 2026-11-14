using Robust.Shared.Map;

namespace Content.Shared.Weather;

public abstract partial class SharedWeatherSystem
{
    /// <summary>Bridges Goob weather scheduling to Wizards weather status effects.</summary>
    public void SetWeather(MapId map, WeatherPrototype? weather, TimeSpan? ending)
    {
        var duration = ending is { } end ? end - Timing.CurTime : (TimeSpan?) null;
        if (duration is { } remaining && remaining < TimeSpan.Zero)
            duration = TimeSpan.Zero;
        TrySetWeather(map, weather is null ? null : "Weather" + weather.ID, out _, duration);
    }
}
