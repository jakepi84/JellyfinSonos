using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinSonos.Services;

/// <summary>
/// Service for interacting with Jellyfin music library.
/// </summary>
public class JellyfinMusicService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IUserManager _userManager;
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
        IUserManager userManager,
        IDtoService dtoService,
        ILogger<JellyfinMusicService> logger)
    {
        _libraryManager = libraryManager;
        _userManager = userManager;
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
        var user = _userManager.GetUserById(userId);
        if (user == null)
        {
            return new List<BaseItem>();
        }

        var query = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.MusicAlbum, BaseItemKind.MusicArtist },
            Recursive = false,
            ParentId = Guid.Empty
        };

        var result = _libraryManager.GetItemsResult(query);
        return result.Items.ToList();
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
        var user = _userManager.GetUserById(userId);
        if (user == null)
        {
            return (new List<MusicArtist>(), 0);
        }

        var query = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.MusicArtist },
            Recursive = true,
            StartIndex = startIndex,
            Limit = limit,
            OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) }
        };

        var result = _libraryManager.GetItemsResult(query);
        return (result.Items.Cast<MusicArtist>().ToList(), result.TotalRecordCount);
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
        var user = _userManager.GetUserById(userId);
        if (user == null)
        {
            return (new List<MusicAlbum>(), 0);
        }

        var query = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.MusicAlbum },
            ArtistIds = new[] { artistId },
            Recursive = true,
            StartIndex = startIndex,
            Limit = limit,
            OrderBy = new[] { (ItemSortBy.ProductionYear, SortOrder.Descending), (ItemSortBy.SortName, SortOrder.Ascending) }
        };

        var result = _libraryManager.GetItemsResult(query);
        return (result.Items.Cast<MusicAlbum>().ToList(), result.TotalRecordCount);
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
        var user = _userManager.GetUserById(userId);
        if (user == null)
        {
            return (new List<MusicAlbum>(), 0);
        }

        var query = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.MusicAlbum },
            Recursive = true,
            StartIndex = startIndex,
            Limit = limit,
            OrderBy = new[] { (ItemSortBy.SortName, SortOrder.Ascending) }
        };

        var result = _libraryManager.GetItemsResult(query);
        return (result.Items.Cast<MusicAlbum>().ToList(), result.TotalRecordCount);
    }

    /// <summary>
    /// Gets tracks for an album.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="albumId">Album ID.</param>
    /// <returns>List of tracks.</returns>
    public async Task<List<Audio>> GetTracksByAlbum(Guid userId, Guid albumId)
    {
        var user = _userManager.GetUserById(userId);
        if (user == null)
        {
            return new List<Audio>();
        }

        var query = new InternalItemsQuery(user)
        {
            IncludeItemTypes = new[] { BaseItemKind.Audio },
            ParentId = albumId,
            Recursive = false,
            OrderBy = new[] { (ItemSortBy.IndexNumber, SortOrder.Ascending) }
        };

        var result = _libraryManager.GetItemsResult(query);
        return result.Items.Cast<Audio>().ToList();
    }

    /// <summary>
    /// Gets a specific item.
    /// </summary>
    /// <param name="userId">User ID.</param>
    /// <param name="itemId">Item ID.</param>
    /// <returns>The item.</returns>
    public async Task<BaseItem?> GetItem(Guid userId, Guid itemId)
    {
        var user = _userManager.GetUserById(userId);
        if (user == null)
        {
            return null;
        }

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
    public async Task<List<BaseItem>> Search(Guid userId, string searchTerm, BaseItemKind[] itemTypes, int limit)
    {
        var user = _userManager.GetUserById(userId);
        if (user == null)
        {
            return new List<BaseItem>();
        }

        var query = new InternalItemsQuery(user)
        {
            SearchTerm = searchTerm,
            IncludeItemTypes = itemTypes,
            Recursive = true,
            Limit = limit
        };

        var result = _libraryManager.GetItemsResult(query);
        return result.Items.ToList();
    }
}
