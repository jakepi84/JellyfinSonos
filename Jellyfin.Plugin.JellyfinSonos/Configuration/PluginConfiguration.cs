using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.JellyfinSonos.Configuration;

/// <summary>
/// Plugin configuration for JellyfinSonos.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the service name displayed in Sonos app.
    /// </summary>
    public string ServiceName { get; set; } = "Jellyfin";

    /// <summary>
    /// Gets or sets the service ID for Sonos.
    /// </summary>
    public int ServiceId { get; set; } = 247;

    /// <summary>
    /// Gets or sets the secret key for signing OAuth tokens.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the external URL for the Jellyfin server (must be accessible by Sonos devices).
    /// </summary>
    public string ExternalUrl { get; set; } = string.Empty;
}
