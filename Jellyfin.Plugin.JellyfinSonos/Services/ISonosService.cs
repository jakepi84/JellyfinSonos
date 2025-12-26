using System.Runtime.Serialization;
using System.ServiceModel;

namespace Jellyfin.Plugin.JellyfinSonos.Services;

/// <summary>
/// SMAPI service interface for Sonos integration.
/// </summary>
[ServiceContract(Namespace = "http://www.sonos.com/Services/1.1")]
public interface ISonosService
{
    /// <summary>
    /// Gets app link for device authorization (used by Sonos to launch OAuth authorize page).
    /// </summary>
    /// <param name="householdId">Household ID.</param>
    /// <returns>App link result.</returns>
    [OperationContract(Action = "http://www.sonos.com/Services/1.1#getAppLink")]
    GetAppLinkResponse GetAppLink(string householdId);

    /// <summary>
    /// Gets metadata for browsing.
    /// </summary>
    /// <param name="id">Item ID.</param>
    /// <param name="index">Start index.</param>
    /// <param name="count">Number of items to return.</param>
    /// <param name="recursive">Whether to recurse.</param>
    /// <returns>Metadata result.</returns>
    [OperationContract(Action = "http://www.sonos.com/Services/1.1#getMetadata")]
    GetMetadataResponse GetMetadata(string id, int index, int count, bool recursive);

    /// <summary>
    /// Gets media metadata for a specific track.
    /// </summary>
    /// <param name="id">Track ID.</param>
    /// <returns>Media metadata result.</returns>
    [OperationContract(Action = "http://www.sonos.com/Services/1.1#getMediaMetadata")]
    GetMediaMetadataResponse GetMediaMetadata(string id);

    /// <summary>
    /// Gets media URI for streaming.
    /// </summary>
    /// <param name="id">Track ID.</param>
    /// <param name="authToken">Optional bearer token to include on playback requests.</param>
    /// <returns>Media URI result.</returns>
    [OperationContract(Action = "http://www.sonos.com/Services/1.1#getMediaURI")]
    GetMediaURIResponse GetMediaURI(string id, string? authToken);

    /// <summary>
    /// Searches for content.
    /// </summary>
    /// <param name="id">Search category ID.</param>
    /// <param name="term">Search term.</param>
    /// <param name="index">Start index.</param>
    /// <param name="count">Number of items to return.</param>
    /// <returns>Search result.</returns>
    [OperationContract(Action = "http://www.sonos.com/Services/1.1#search")]
    SearchResponse Search(string id, string term, int index, int count);

    /// <summary>
    /// Reports account action (used for logout, etc.).
    /// </summary>
    /// <param name="type">Action type.</param>
    [OperationContract(Action = "http://www.sonos.com/Services/1.1#reportAccountAction")]
    void ReportAccountAction(string type);
}

/// <summary>
/// Response for GetAppLink.
/// </summary>
[DataContract]
public class GetAppLinkResponse
{
    /// <summary>
    /// Gets or sets the authorize account info.
    /// </summary>
    [DataMember]
    public AuthorizeAccount? AuthorizeAccount { get; set; }
}

/// <summary>
/// Authorize account info.
/// </summary>
[DataContract]
public class AuthorizeAccount
{
    /// <summary>
    /// Gets or sets the app URL string ID.
    /// </summary>
    [DataMember]
    public string AppUrlStringId { get; set; } = "AppLinkMessage";

    /// <summary>
    /// Gets or sets the device link info.
    /// </summary>
    [DataMember]
    public DeviceLink? DeviceLink { get; set; }
}

/// <summary>
/// Device link info.
/// </summary>
[DataContract]
public class DeviceLink
{
    /// <summary>
    /// Gets or sets the registration URL.
    /// </summary>
    [DataMember]
    public string RegUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the link code.
    /// </summary>
    [DataMember]
    public string LinkCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to show the link code.
    /// </summary>
    [DataMember]
    public bool ShowLinkCode { get; set; }
}

/// <summary>
/// Response for GetMetadata.
/// </summary>
[DataContract]
public class GetMetadataResponse
{
    /// <summary>
    /// Gets or sets the index.
    /// </summary>
    [DataMember]
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the count.
    /// </summary>
    [DataMember]
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the total.
    /// </summary>
    [DataMember]
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the media collections (albums, artists, etc.).
    /// </summary>
    [DataMember]
    public List<MediaCollection>? MediaCollection { get; set; }

    /// <summary>
    /// Gets or sets the media metadata (tracks).
    /// </summary>
    [DataMember]
    public List<MediaMetadata>? MediaMetadata { get; set; }
}

/// <summary>
/// Media collection (album, artist, playlist).
/// </summary>
[DataContract]
public class MediaCollection
{
    /// <summary>
    /// Gets or sets the ID.
    /// </summary>
    [DataMember]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [DataMember]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the item type.
    /// </summary>
    [DataMember]
    public string ItemType { get; set; } = "collection";

    /// <summary>
    /// Gets or sets the artist name.
    /// </summary>
    [DataMember]
    public string? Artist { get; set; }

    /// <summary>
    /// Gets or sets the album art URI.
    /// </summary>
    [DataMember]
    public string? AlbumArtURI { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this can be played.
    /// </summary>
    [DataMember]
    public bool CanPlay { get; set; }
}

/// <summary>
/// Media metadata (track).
/// </summary>
[DataContract]
public class MediaMetadata
{
    /// <summary>
    /// Gets or sets the ID.
    /// </summary>
    [DataMember]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    [DataMember]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the mime type.
    /// </summary>
    [DataMember]
    public string MimeType { get; set; } = "audio/mp3";

    /// <summary>
    /// Gets or sets the item type.
    /// </summary>
    [DataMember]
    public string ItemType { get; set; } = "track";

    /// <summary>
    /// Gets or sets the track number.
    /// </summary>
    [DataMember]
    public int? TrackNumber { get; set; }

    /// <summary>
    /// Gets or sets the artist.
    /// </summary>
    [DataMember]
    public string? Artist { get; set; }

    /// <summary>
    /// Gets or sets the album.
    /// </summary>
    [DataMember]
    public string? Album { get; set; }

    /// <summary>
    /// Gets or sets the album art URI.
    /// </summary>
    [DataMember]
    public string? AlbumArtURI { get; set; }

    /// <summary>
    /// Gets or sets the duration in seconds.
    /// </summary>
    [DataMember]
    public int? Duration { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this can be played.
    /// </summary>
    [DataMember]
    public bool CanPlay { get; set; } = true;
}

/// <summary>
/// Response for GetMediaMetadata.
/// </summary>
[DataContract]
public class GetMediaMetadataResponse
{
    /// <summary>
    /// Gets or sets the media metadata.
    /// </summary>
    [DataMember]
    public MediaMetadata? MediaMetadata { get; set; }
}

/// <summary>
/// Response for GetMediaURI.
/// </summary>
[DataContract]
public class GetMediaURIResponse
{
    /// <summary>
    /// Gets or sets the media URI.
    /// </summary>
    [DataMember]
    public string? MediaUri { get; set; }

    /// <summary>
    /// Gets or sets the HTTP headers.
    /// </summary>
    [DataMember]
    public List<HttpHeader>? HttpHeaders { get; set; }
}

/// <summary>
/// HTTP header.
/// </summary>
[DataContract]
public class HttpHeader
{
    /// <summary>
    /// Gets or sets the header name.
    /// </summary>
    [DataMember]
    public string Header { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the header value.
    /// </summary>
    [DataMember]
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Response for Search.
/// </summary>
[DataContract]
public class SearchResponse
{
    /// <summary>
    /// Gets or sets the index.
    /// </summary>
    [DataMember]
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the count.
    /// </summary>
    [DataMember]
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the total.
    /// </summary>
    [DataMember]
    public int Total { get; set; }

    /// <summary>
    /// Gets or sets the media collections.
    /// </summary>
    [DataMember]
    public List<MediaCollection>? MediaCollection { get; set; }

    /// <summary>
    /// Gets or sets the media metadata.
    /// </summary>
    [DataMember]
    public List<MediaMetadata>? MediaMetadata { get; set; }
}
