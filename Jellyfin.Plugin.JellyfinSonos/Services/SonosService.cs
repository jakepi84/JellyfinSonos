using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.JellyfinSonos.Configuration;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Model.Dto;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinSonos.Services;

/// <summary>
/// Implementation of Sonos SMAPI service.
/// </summary>
public class SonosService : ISonosService
{
    private readonly JellyfinMusicService _musicService;
    private readonly LinkCodeService _linkCodeService;
    private readonly ILogger<SonosService> _logger;
    private readonly PluginConfiguration _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="SonosService"/> class.
    /// </summary>
    /// <param name="musicService">Music service.</param>
    /// <param name="linkCodeService">Link code service.</param>
    /// <param name="logger">Logger.</param>
    public SonosService(
        JellyfinMusicService musicService,
        LinkCodeService linkCodeService,
        ILogger<SonosService> logger)
    {
        _musicService = musicService;
        _linkCodeService = linkCodeService;
        _logger = logger;
        _config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
    }

    /// <inheritdoc />
    public GetAppLinkResponse GetAppLink(string householdId)
    {
        var linkCode = _linkCodeService.GenerateLinkCode();
        var baseUrl = _config.ExternalUrl;

        return new GetAppLinkResponse
        {
            AuthorizeAccount = new AuthorizeAccount
            {
                AppUrlStringId = "AppLinkMessage",
                DeviceLink = new DeviceLink
                {
                    RegUrl = $"{baseUrl}/sonos/login?linkCode={linkCode}",
                    LinkCode = linkCode,
                    ShowLinkCode = false
                }
            }
        };
    }

    /// <inheritdoc />
    public GetDeviceAuthTokenResponse GetDeviceAuthToken(string householdId, string linkCode, string linkDeviceId)
    {
        var credentials = _linkCodeService.GetCredentials(linkCode);
        if (credentials == null)
        {
            throw new Exception("Invalid or expired link code");
        }

        // Create auth token with username embedded
        var token = CreateAuthToken(credentials.Value.Username, credentials.Value.Password);

        return new GetDeviceAuthTokenResponse
        {
            AuthToken = token,
            PrivateKey = "jellyfin",
            UserInfo = new UserInfo
            {
                Nickname = credentials.Value.Username
            }
        };
    }

    /// <inheritdoc />
    public GetMetadataResponse GetMetadata(string id, int index, int count, bool recursive)
    {
        try
        {
            // Parse the ID to determine what to return
            if (string.IsNullOrEmpty(id) || id == "root")
            {
                // Return root categories
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

            var parts = id.Split(':');
            var type = parts[0];
            
            // This is a simplified implementation
            // In a real implementation, you'd need to extract user ID from credentials
            // and make async calls to the music service
            
            return new GetMetadataResponse
            {
                Index = 0,
                Count = 0,
                Total = 0,
                MediaCollection = new List<MediaCollection>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMetadata");
            throw;
        }
    }

    /// <inheritdoc />
    public GetMediaMetadataResponse GetMediaMetadata(string id)
    {
        try
        {
            // Parse ID and return track metadata
            // This is a simplified implementation
            return new GetMediaMetadataResponse
            {
                MediaMetadata = new MediaMetadata
                {
                    Id = id,
                    Title = "Track",
                    ItemType = "track",
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
    public GetMediaURIResponse GetMediaURI(string id)
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
                HttpHeaders = new List<HttpHeader>()
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
        try
        {
            // Search implementation
            return new SearchResponse
            {
                Index = 0,
                Count = 0,
                Total = 0,
                MediaCollection = new List<MediaCollection>()
            };
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

    private string CreateAuthToken(string username, string password)
    {
        // Create a simple token with username:password:timestamp
        var data = $"{username}:{password}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var key = Encoding.UTF8.GetBytes(_config.SecretKey.PadRight(32).Substring(0, 32));
        
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(data);
        var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        // Combine IV and encrypted data
        var result = new byte[aes.IV.Length + encrypted.Length];
        Array.Copy(aes.IV, 0, result, 0, aes.IV.Length);
        Array.Copy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        
        return Convert.ToBase64String(result);
    }

    private (string Username, string Password)? DecryptAuthToken(string token)
    {
        try
        {
            var data = Convert.FromBase64String(token);
            var key = Encoding.UTF8.GetBytes(_config.SecretKey.PadRight(32).Substring(0, 32));
            
            using var aes = Aes.Create();
            aes.Key = key;
            
            var iv = new byte[aes.IV.Length];
            var encrypted = new byte[data.Length - iv.Length];
            Array.Copy(data, 0, iv, 0, iv.Length);
            Array.Copy(data, iv.Length, encrypted, 0, encrypted.Length);
            aes.IV = iv;
            
            using var decryptor = aes.CreateDecryptor();
            var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
            var decryptedText = Encoding.UTF8.GetString(decrypted);
            
            var parts = decryptedText.Split(':');
            if (parts.Length >= 2)
            {
                return (parts[0], parts[1]);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }
}
