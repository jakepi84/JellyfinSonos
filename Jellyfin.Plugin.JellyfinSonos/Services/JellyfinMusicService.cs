using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Jellyfin.Plugin.JellyfinSonos.Services;

/// <summary>
/// Service for interacting with Jellyfin music library.
/// </summary>
public class JellyfinMusicService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IDtoService _dtoService;
    private readonly ILogger<JellyfinMusicService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JellyfinMusicService"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="userManager">User manager.</param>
    /// <param name="dtoService">DTO service.</param>
    /// <param name="logger">Logger.</param>
    public JellyfinMusicService(
        ILibraryManager libraryManager,
        IDtoService dtoService,
        ILogger<JellyfinMusicService> logger)
    {
        _libraryManager = libraryManager;
        _dtoService = dtoService;
        _logger = logger;
    }



    /// <summary>
    /// Gets the root music items for browsing.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <returns>List of root items.</returns>
    public async Task<List<BaseItem>> GetRootItems(Guid userId)
    {
        // Avoid referencing Jellyfin User or enum types; Sonos root is virtual.
        return new List<BaseItem>();
    }

    /// <summary>
    /// Gets playlists.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="startIndex">Start index.</param>
    /// <param name="limit">Limit.</param>
    /// <returns>List of playlists.</returns>
    public async Task<(List<BaseItem> Items, int TotalCount)> GetPlaylists(Guid userId, int startIndex, int limit)
    {
        try
        {
            var query = new InternalItemsQuery
            {
                Recursive = true,
                StartIndex = 0,
                Limit = Math.Max(limit * 5, 500)
            };
            TrySetIncludeItemTypes(query, "Playlist");
            // Scope playlists to the authenticated user when supported by the query
            TrySetUserScope(query, userId);

            var result = _libraryManager.GetItemsResult(query);
            
            // Manually filter playlists by checking Shares property
            var userFilteredPlaylists = result.Items.Where(p =>
            {
                // Check if the playlist has a Shares property we can use to filter
                var sharesProperty = p.GetType().GetProperty("Shares", BindingFlags.Public | BindingFlags.Instance);
                if (sharesProperty != null)
                {
                    var shares = sharesProperty.GetValue(p);
                    if (shares != null)
                    {
                        // Check if shares collection contains this user
                        var sharesType = shares.GetType();
                        if (sharesType.IsGenericType)
                        {
                            var anyMethod = sharesType.GetMethod("Any", new[] { typeof(Func<,>).MakeGenericType(sharesType.GetGenericArguments()[0], typeof(bool)) });
                            // For now, just include all playlists if we can't filter properly
                        }
                    }
                }
                
                // Check for UserId property on the playlist itself
                var userIdProperty = p.GetType().GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance);
                if (userIdProperty != null && userIdProperty.PropertyType == typeof(Guid))
                {
                    var playlistUserId = (Guid)userIdProperty.GetValue(p);
                    return playlistUserId == userId;
                }
                
                // If we can't determine ownership, include it (for now)
                return true;
            }).ToList();
            
            _logger.LogInformation("Filtered playlists: {FilteredCount} from {TotalCount} for user {UserId}", 
                userFilteredPlaylists.Count, result.Items.Count(), userId);
            
            // Deduplicate playlists by name
            var deduplicatedPlaylists = userFilteredPlaylists
                .GroupBy(p => p.Name?.ToLowerInvariant()?.Trim() ?? string.Empty)
                .Select(g => g.First())
                .Skip(startIndex)
                .Take(limit)
                .ToList();
            var totalPlaylists = userFilteredPlaylists.Count();
            return (deduplicatedPlaylists, totalPlaylists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playlists, returning empty");
            return (new List<BaseItem>(), 0);
        }
    }

    /// <summary>
    /// Gets items within a playlist.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="playlistId">Playlist ID.</param>
    /// <returns>List of playlist items.</returns>
    public async Task<List<BaseItem>> GetPlaylistItems(Guid userId, Guid playlistId)
    {
        try
        {
            var query = new InternalItemsQuery
            {
                Recursive = true,
                StartIndex = 0,
                Limit = 10000
            };
            TrySetIncludeItemTypes(query, "Audio");
            // Playlist items are not children; set PlaylistId on the query when available
            TrySetPlaylistId(query, playlistId);
            // Scope to user if supported
            TrySetUserScope(query, userId);

            var result = _libraryManager.GetItemsResult(query);
            return result.Items.OfType<Audio>().Cast<BaseItem>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting playlist items, returning empty");
            return new List<BaseItem>();
        }
    }

    /// <summary>
    /// Gets artists.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="startIndex">Start index.</param>
    /// <param name="limit">Limit.</param>
    /// <returns>List of artists.</returns>
    public async Task<(List<MusicArtist> Items, int TotalCount)> GetArtists(Guid userId, int startIndex, int limit)
    {
        try
        {
            var query = new InternalItemsQuery
            {
                Recursive = true,
                StartIndex = 0,
                Limit = Math.Max(limit * 10, 1000)
            };
            TrySetIncludeItemTypes(query, "MusicArtist");

            var result = _libraryManager.GetItemsResult(query);
            var artists = result.Items.OfType<MusicArtist>().Skip(startIndex).Take(limit).ToList();
            var totalArtists = result.Items.OfType<MusicArtist>().Count();
            return (artists, totalArtists);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting artists, returning empty");
            return (new List<MusicArtist>(), 0);
        }
    }

    /// <summary>
    /// Gets albums for an artist.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="artistId">Artist ID.</param>
    /// <param name="startIndex">Start index.</param>
    /// <param name="limit">Limit.</param>
    /// <returns>List of albums.</returns>
    public async Task<(List<MusicAlbum> Items, int TotalCount)> GetAlbumsByArtist(Guid userId, Guid artistId, int startIndex, int limit)
    {
        try
        {
            var query = new InternalItemsQuery
            {
                ArtistIds = new[] { artistId },
                Recursive = true,
                StartIndex = 0,
                Limit = Math.Max(limit * 5, 500)
            };
            TrySetIncludeItemTypes(query, "MusicAlbum");

            var result = _libraryManager.GetItemsResult(query);
            var albums = result.Items.OfType<MusicAlbum>().Skip(startIndex).Take(limit).ToList();
            var totalAlbums = result.Items.OfType<MusicAlbum>().Count();
            return (albums, totalAlbums);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting albums by artist, returning empty");
            return (new List<MusicAlbum>(), 0);
        }
    }

    /// <summary>
    /// Gets all albums.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="startIndex">Start index.</param>
    /// <param name="limit">Limit.</param>
    /// <returns>List of albums.</returns>
    public async Task<(List<MusicAlbum> Items, int TotalCount)> GetAlbums(Guid userId, int startIndex, int limit)
    {
        try
        {
            var query = new InternalItemsQuery
            {
                Recursive = true,
                StartIndex = 0,
                Limit = Math.Max(limit * 5, 500)
            };
            TrySetIncludeItemTypes(query, "MusicAlbum");

            var result = _libraryManager.GetItemsResult(query);
            var albums = result.Items.OfType<MusicAlbum>().Skip(startIndex).Take(limit).ToList();
            var totalAlbums = result.Items.OfType<MusicAlbum>().Count();
            return (albums, totalAlbums);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting albums, returning empty");
            return (new List<MusicAlbum>(), 0);
        }
    }

    /// <summary>
    /// Gets tracks for an album.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="albumId">Album ID.</param>
    /// <returns>List of tracks.</returns>
    public async Task<List<Audio>> GetTracksByAlbum(Guid userId, Guid albumId)
    {
        var query = new InternalItemsQuery
        {
            ParentId = albumId,
            Recursive = false
        };
        TrySetIncludeItemTypes(query, "Audio");

        try
        {
            var result = _libraryManager.GetItemsResult(query);
            return result.Items.OfType<Audio>().ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracks by album, returning empty");
            return new List<Audio>();
        }
    }

    /// <summary>
    /// Gets a specific item.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="itemId">Item ID.</param>
    /// <returns>The item.</returns>
    public async Task<BaseItem?> GetItem(Guid userId, Guid itemId)
    {
        return _libraryManager.GetItemById(itemId);
    }

    /// <summary>
    /// Searches for items.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="searchTerm">Search term.</param>
    /// <param name="itemTypes">Item types to search.</param>
    /// <param name="limit">Limit.</param>
    /// <returns>Search results.</returns>
    public async Task<List<BaseItem>> Search(Guid userId, string searchTerm, int limit)
    {
        try
        {
            var query = new InternalItemsQuery
            {
                SearchTerm = searchTerm,
                Recursive = true,
                Limit = Math.Max(limit * 3, 300)
            };

            var result = _libraryManager.GetItemsResult(query);
            return result.Items.Take(limit).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching, returning empty");
            return new List<BaseItem>();
        }
    }

    /// <summary>
    /// Searches for items of specific type names using reflection to set IncludeItemTypes.
    /// </summary>
    public async Task<List<BaseItem>> SearchByType(Guid userId, string searchTerm, string[] typeNames, int limit)
    {
        try
        {
            var query = new InternalItemsQuery
            {
                SearchTerm = searchTerm,
                Recursive = true,
                Limit = Math.Max(limit * 3, 300)
            };

            TrySetIncludeItemTypes(query, typeNames);

            var result = _libraryManager.GetItemsResult(query);
            return result.Items.Take(limit).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching by type, returning empty");
            return new List<BaseItem>();
        }
    }

    /// <summary>
    /// Uses reflection to set IncludeItemTypes without directly referencing Jellyfin.Data.Enums.
    /// </summary>
    private void TrySetIncludeItemTypes(InternalItemsQuery query, params string[] typeNames)
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var dataAssembly = assemblies.FirstOrDefault(a => a.GetType("Jellyfin.Data.Enums.BaseItemKind") != null);
            if (dataAssembly == null)
            {
                _logger.LogWarning("Jellyfin.Data.Enums.BaseItemKind not found in AppDomain assemblies");
                return;
            }

            var enumType = dataAssembly.GetType("Jellyfin.Data.Enums.BaseItemKind");
            if (enumType == null)
            {
                _logger.LogWarning("BaseItemKind type not resolved");
                return;
            }

            var values = Array.CreateInstance(enumType, typeNames.Length);
            for (int i = 0; i < typeNames.Length; i++)
            {
                var val = Enum.Parse(enumType, typeNames[i], true);
                values.SetValue(val, i);
            }

            var prop = typeof(InternalItemsQuery).GetProperty("IncludeItemTypes", BindingFlags.Public | BindingFlags.Instance);
            prop?.SetValue(query, values);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set IncludeItemTypes via reflection");
        }
    }

    /// <summary>
    /// Tries to scope an InternalItemsQuery to the specified user using available properties.
    /// </summary>
    private void TrySetUserScope(InternalItemsQuery query, Guid userId)
    {
        try
        {
            var qType = typeof(InternalItemsQuery);
            var userIdProp = qType.GetProperty("UserId", BindingFlags.Public | BindingFlags.Instance);
            if (userIdProp != null && userIdProp.PropertyType == typeof(Guid))
            {
                userIdProp.SetValue(query, userId);
                return;
            }

            var userIdsProp = qType.GetProperty("UserIds", BindingFlags.Public | BindingFlags.Instance);
            if (userIdsProp != null && userIdsProp.PropertyType.IsArray && userIdsProp.PropertyType.GetElementType() == typeof(Guid))
            {
                userIdsProp.SetValue(query, new Guid[] { userId });
                return;
            }

            _logger.LogDebug("InternalItemsQuery has no UserId/UserIds properties to scope results");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to set user scope on InternalItemsQuery");
        }
    }

    /// <summary>
    /// Tries to set playlist scoping on InternalItemsQuery using available properties.
    /// </summary>
    private void TrySetPlaylistId(InternalItemsQuery query, Guid playlistId)
    {
        try
        {
            var qType = typeof(InternalItemsQuery);
            var names = new[] { "PlaylistId", "PlaylistIds" };
            foreach (var name in names)
            {
                var p = qType.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                if (p == null)
                {
                    continue;
                }

                if (p.PropertyType == typeof(Guid))
                {
                    p.SetValue(query, playlistId);
                    return;
                }

                if (p.PropertyType.IsArray && p.PropertyType.GetElementType() == typeof(Guid))
                {
                    p.SetValue(query, new Guid[] { playlistId });
                    return;
                }
            }

            _logger.LogDebug("InternalItemsQuery has no PlaylistId/PlaylistIds properties; playlist items query may return empty");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to set playlist scope on InternalItemsQuery");
        }
    }
}
