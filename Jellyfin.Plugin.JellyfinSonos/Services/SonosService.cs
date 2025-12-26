using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.JellyfinSonos.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinSonos.Services;

/// <summary>
/// Implementation of Sonos SMAPI service.
/// </summary>
public class SonosService : ISonosService
{
    private readonly JellyfinMusicService _musicService;
    private readonly IUserManager _userManager;
    private readonly OAuthService _oauthService;
    private readonly ILogger<SonosService> _logger;
    private readonly PluginConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="SonosService"/> class.
    /// </summary>
    /// <param name="musicService">Music service.</param>
    /// <param name="userManager">User manager.</param>
    /// <param name="oauthService">OAuth token service.</param>
    /// <param name="logger">Logger.</param>
    public SonosService(
        JellyfinMusicService musicService,
        IUserManager userManager,
        OAuthService oauthService,
        ILogger<SonosService> logger)
    {
        _musicService = musicService;
        _userManager = userManager;
        _oauthService = oauthService;
        _logger = logger;
        _config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
    }

    /// <inheritdoc />
    public GetAppLinkResponse GetAppLink(string householdId)
    {
        var baseUrl = _config.ExternalUrl?.TrimEnd('/') ?? string.Empty;
        var regUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? string.Empty
            : $"{baseUrl}/sonos/oauth/authorize";

        // Sonos still calls getAppLink even for OAuth; we return the authorize page URL and no link code.
        return new GetAppLinkResponse
        {
            AuthorizeAccount = new AuthorizeAccount
            {
                AppUrlStringId = "AppLinkMessage",
                DeviceLink = new DeviceLink
                {
                    RegUrl = regUrl,
                    LinkCode = string.Empty,
                    ShowLinkCode = false
                }
            }
        };
    }

    /// <summary>
    /// Extracts user ID from auth token.
    /// </summary>
    /// <param name="authToken">Auth token.</param>
    /// <returns>User ID or null.</returns>
    private Guid? GetUserIdFromToken(string? authToken)
    {
        try
        {
            if (!_oauthService.ValidateAccessToken(authToken, out var principal))
            {
                return null;
            }

            var user = _userManager.GetUserByName(principal.Username);
            return user?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting user from token");
            return null;
        }
    }

    /// <inheritdoc />
    public GetMetadataResponse GetMetadata(string id, int index, int count, bool recursive)
    {
        // This method is called without auth context - return root only
        return GetMetadata(id, index, count, recursive, null);
    }

    /// <summary>
    /// Gets metadata with auth token.
    /// </summary>
    /// <param name="id">Item ID.</param>
    /// <param name="index">Start index.</param>
    /// <param name="count">Number of items to return.</param>
    /// <param name="recursive">Whether to recurse.</param>
    /// <param name="authToken">Optional auth token for user context.</param>
    /// <returns>Metadata result.</returns>
    public GetMetadataResponse GetMetadata(string id, int index, int count, bool recursive, string? authToken)
    {
        try
        {
            _logger.LogDebug("GetMetadata called for id: {Id}, index: {Index}, count: {Count}", id, index, count);

            // Return root categories if no ID specified
            if (string.IsNullOrEmpty(id) || id == "root")
            {
                return new GetMetadataResponse
                {
                    Index = 0,
                    Count = 3,
                    Total = 3,
                    MediaCollection = new List<MediaCollection>
                    {
                        new MediaCollection
                        {
                            Id = "artists",
                            Title = "Artists",
                            ItemType = "collection",
                            CanPlay = false
                        },
                        new MediaCollection
                        {
                            Id = "albums",
                            Title = "Albums",
                            ItemType = "collection",
                            CanPlay = false
                        },
                        new MediaCollection
                        {
                            Id = "search",
                            Title = "Search",
                            ItemType = "search",
                            CanPlay = false
                        }
                    }
                };
            }

            // Get user ID from auth token
            var userId = GetUserIdFromToken(authToken ?? string.Empty);
            if (!userId.HasValue)
            {
                _logger.LogWarning("No valid user context for GetMetadata");
                return new GetMetadataResponse { Index = 0, Count = 0, Total = 0 };
            }

            // Parse the ID to determine what to return
            var parts = id.Split(':');
            var type = parts[0];

            switch (type)
            {
                case "artists":
                    return GetArtists(userId.Value, index, count).Result;

                case "albums":
                    return GetAllAlbums(userId.Value, index, count).Result;

                case "artist":
                    if (parts.Length > 1 && Guid.TryParse(parts[1], out var artistId))
                    {
                        return GetAlbumsByArtist(userId.Value, artistId, index, count).Result;
                    }
                    break;

                case "album":
                    if (parts.Length > 1 && Guid.TryParse(parts[1], out var albumId))
                    {
                        return GetTracksByAlbum(userId.Value, albumId).Result;
                    }
                    break;
            }

            _logger.LogWarning("Unknown metadata type: {Type}", type);
            return new GetMetadataResponse { Index = 0, Count = 0, Total = 0 };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMetadata");
            throw;
        }
    }

    private async Task<GetMetadataResponse> GetArtists(Guid userId, int startIndex, int count)
    {
        var result = await _musicService.GetArtists(userId, startIndex, count);
        var baseUrl = _config.ExternalUrl;

        return new GetMetadataResponse
        {
            Index = startIndex,
            Count = result.Items.Count,
            Total = result.TotalCount,
            MediaCollection = result.Items.Select(artist => new MediaCollection
            {
                Id = $"artist:{artist.Id}",
                Title = artist.Name,
                ItemType = "artist",
                AlbumArtURI = GetImageUrl(baseUrl, artist.Id),
                CanPlay = false
            }).ToList()
        };
    }

    private async Task<GetMetadataResponse> GetAllAlbums(Guid userId, int startIndex, int count)
    {
        var result = await _musicService.GetAlbums(userId, startIndex, count);
        var baseUrl = _config.ExternalUrl;

        return new GetMetadataResponse
        {
            Index = startIndex,
            Count = result.Items.Count,
            Total = result.TotalCount,
            MediaCollection = result.Items.Select(album => new MediaCollection
            {
                Id = $"album:{album.Id}",
                Title = album.Name,
                ItemType = "album",
                Artist = album.AlbumArtist,
                AlbumArtURI = GetImageUrl(baseUrl, album.Id),
                CanPlay = true
            }).ToList()
        };
    }

    private async Task<GetMetadataResponse> GetAlbumsByArtist(Guid userId, Guid artistId, int startIndex, int count)
    {
        var result = await _musicService.GetAlbumsByArtist(userId, artistId, startIndex, count);
        var baseUrl = _config.ExternalUrl;

        return new GetMetadataResponse
        {
            Index = startIndex,
            Count = result.Items.Count,
            Total = result.TotalCount,
            MediaCollection = result.Items.Select(album => new MediaCollection
            {
                Id = $"album:{album.Id}",
                Title = album.Name,
                ItemType = "album",
                Artist = album.AlbumArtist,
                AlbumArtURI = GetImageUrl(baseUrl, album.Id),
                CanPlay = true
            }).ToList()
        };
    }

    private async Task<GetMetadataResponse> GetTracksByAlbum(Guid userId, Guid albumId)
    {
        var tracks = await _musicService.GetTracksByAlbum(userId, albumId);
        var baseUrl = _config.ExternalUrl;

        return new GetMetadataResponse
        {
            Index = 0,
            Count = tracks.Count,
            Total = tracks.Count,
            MediaMetadata = tracks.Select(track => new MediaMetadata
            {
                Id = $"track:{track.Id}",
                Title = track.Name,
                ItemType = "track",
                MimeType = GetMimeType(track.Path),
                TrackNumber = track.IndexNumber,
                Artist = track.AlbumArtists?.FirstOrDefault(),
                Album = track.Album,
                AlbumArtURI = track.ParentId != Guid.Empty ? GetImageUrl(baseUrl, track.ParentId) : null,
                Duration = (int?)track.RunTimeTicks / 10000000, // Convert to seconds
                CanPlay = true
            }).ToList()
        };
    }

    private string GetImageUrl(string baseUrl, Guid itemId)
    {
        return $"{baseUrl}/Items/{itemId}/Images/Primary?maxWidth=300";
    }

    private string GetMimeType(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "audio/mpeg";
        }

        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            ".wav" => "audio/wav",
            _ => "audio/mpeg"
        };
    }

    /// <inheritdoc />
    public GetMediaMetadataResponse GetMediaMetadata(string id)
    {
        return GetMediaMetadata(id, null);
    }

    /// <summary>
    /// Gets media metadata with auth token.
    /// </summary>
    /// <param name="id">Track ID.</param>
    /// <param name="authToken">Optional auth token.</param>
    /// <returns>Media metadata.</returns>
    public GetMediaMetadataResponse GetMediaMetadata(string id, string? authToken)
    {
        try
        {
            var parts = id.Split(':');
            if (parts.Length < 2 || parts[0] != "track")
            {
                throw new Exception("Invalid track ID");
            }

            var userId = GetUserIdFromToken(authToken ?? string.Empty);
            if (!userId.HasValue)
            {
                _logger.LogWarning("No valid user context for GetMediaMetadata");
                return new GetMediaMetadataResponse();
            }

            if (!Guid.TryParse(parts[1], out var trackId))
            {
                throw new Exception("Invalid track GUID");
            }

            var track = _musicService.GetItem(userId.Value, trackId).Result as Audio;
            if (track == null)
            {
                return new GetMediaMetadataResponse();
            }

            var baseUrl = _config.ExternalUrl;

            return new GetMediaMetadataResponse
            {
                MediaMetadata = new MediaMetadata
                {
                    Id = id,
                    Title = track.Name,
                    ItemType = "track",
                    MimeType = GetMimeType(track.Path),
                    TrackNumber = track.IndexNumber,
                    Artist = track.AlbumArtists?.FirstOrDefault(),
                    Album = track.Album,
                    AlbumArtURI = track.ParentId != Guid.Empty ? GetImageUrl(baseUrl, track.ParentId) : null,
                    Duration = (int?)track.RunTimeTicks / 10000000,
                    CanPlay = true
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMediaMetadata");
            throw;
        }
    }

    /// <inheritdoc />
    public GetMediaURIResponse GetMediaURI(string id, string? authToken)
    {
        try
        {
            var parts = id.Split(':');
            if (parts.Length < 2 || parts[0] != "track")
            {
                throw new Exception("Invalid track ID");
            }

            var trackId = parts[1];
            var baseUrl = _config.ExternalUrl;

            return new GetMediaURIResponse
            {
                MediaUri = $"{baseUrl}/sonos/stream/{trackId}",
                HttpHeaders = string.IsNullOrWhiteSpace(authToken)
                    ? new List<HttpHeader>()
                    : new List<HttpHeader>
                    {
                        new HttpHeader
                        {
                            Header = "Authorization",
                            Value = $"Bearer {authToken}"
                        }
                    }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMediaURI");
            throw;
        }
    }

    /// <inheritdoc />
    public SearchResponse Search(string id, string term, int index, int count)
    {
        return Search(id, term, index, count, null);
    }

    /// <summary>
    /// Search with auth token.
    /// </summary>
    /// <param name="id">Search category ID.</param>
    /// <param name="term">Search term.</param>
    /// <param name="index">Start index.</param>
    /// <param name="count">Number of items.</param>
    /// <param name="authToken">Optional auth token.</param>
    /// <returns>Search results.</returns>
    public SearchResponse Search(string id, string term, int index, int count, string? authToken)
    {
        try
        {
            _logger.LogDebug("Search called: id={Id}, term={Term}", id, term);

            var userId = GetUserIdFromToken(authToken ?? string.Empty);
            if (!userId.HasValue)
            {
                _logger.LogWarning("No valid user context for Search");
                return new SearchResponse { Index = 0, Count = 0, Total = 0 };
            }

            var baseUrl = _config.ExternalUrl;

            switch (id)
            {
                case "artists":
                    var artists = _musicService.Search(userId.Value, term, new[] { BaseItemKind.MusicArtist }, count).Result;
                    return new SearchResponse
                    {
                        Index = 0,
                        Count = artists.Count,
                        Total = artists.Count,
                        MediaCollection = artists.Cast<MusicArtist>().Select(artist => new MediaCollection
                        {
                            Id = $"artist:{artist.Id}",
                            Title = artist.Name,
                            ItemType = "artist",
                            AlbumArtURI = GetImageUrl(baseUrl, artist.Id),
                            CanPlay = false
                        }).ToList()
                    };

                case "albums":
                    var albums = _musicService.Search(userId.Value, term, new[] { BaseItemKind.MusicAlbum }, count).Result;
                    return new SearchResponse
                    {
                        Index = 0,
                        Count = albums.Count,
                        Total = albums.Count,
                        MediaCollection = albums.Cast<MusicAlbum>().Select(album => new MediaCollection
                        {
                            Id = $"album:{album.Id}",
                            Title = album.Name,
                            ItemType = "album",
                            Artist = album.AlbumArtist,
                            AlbumArtURI = GetImageUrl(baseUrl, album.Id),
                            CanPlay = true
                        }).ToList()
                    };

                case "tracks":
                    var tracks = _musicService.Search(userId.Value, term, new[] { BaseItemKind.Audio }, count).Result;
                    return new SearchResponse
                    {
                        Index = 0,
                        Count = tracks.Count,
                        Total = tracks.Count,
                        MediaMetadata = tracks.Cast<Audio>().Select(track => new MediaMetadata
                        {
                            Id = $"track:{track.Id}",
                            Title = track.Name,
                            ItemType = "track",
                            MimeType = GetMimeType(track.Path),
                            Artist = track.AlbumArtists?.FirstOrDefault(),
                            Album = track.Album,
                            AlbumArtURI = track.ParentId != Guid.Empty ? GetImageUrl(baseUrl, track.ParentId) : null,
                            CanPlay = true
                        }).ToList()
                    };

                default:
                    _logger.LogWarning("Unknown search type: {Id}", id);
                    return new SearchResponse { Index = 0, Count = 0, Total = 0 };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Search");
            throw;
        }
    }

    /// <inheritdoc />
    public void ReportAccountAction(string type)
    {
        _logger.LogInformation("Account action: {Type}", type);
    }
}
