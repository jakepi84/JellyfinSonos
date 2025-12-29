using Jellyfin.Plugin.JellyfinSonos.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinSonos.Services;

/// <summary>
/// Provides access to the plugin configuration, always fetching the latest version from the plugin instance.
/// </summary>
public interface IConfigurationProvider
{
    /// <summary>
    /// Gets the current plugin configuration.
    /// </summary>
    /// <returns>The plugin configuration.</returns>
    PluginConfiguration GetConfiguration();
}

/// <summary>
/// Default implementation of configuration provider that accesses the plugin instance.
/// </summary>
public class ConfigurationProvider : IConfigurationProvider
{
    private readonly ILogger<ConfigurationProvider> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationProvider"/> class.
    /// </summary>
    /// <param name="logger">Logger.</param>
    public ConfigurationProvider(ILogger<ConfigurationProvider> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public PluginConfiguration GetConfiguration()
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        _logger.LogDebug("Retrieved configuration: ExternalUrl={ExternalUrl}, ServiceName={ServiceName}, ServiceId={ServiceId}", 
            config.ExternalUrl, config.ServiceName, config.ServiceId);
        return config;
    }
}
