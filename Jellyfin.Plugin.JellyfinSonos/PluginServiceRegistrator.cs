using Jellyfin.Plugin.JellyfinSonos.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.JellyfinSonos;

/// <summary>
/// Plugin service registrator.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Register services as singletons
        serviceCollection.AddSingleton<OAuthService>();
        serviceCollection.AddSingleton<JellyfinMusicService>();
        serviceCollection.AddSingleton<SonosService>();
    }
}
