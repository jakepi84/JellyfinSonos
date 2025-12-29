using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Jellyfin.Plugin.JellyfinSonos.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinSonos.Api;

/// <summary>
/// SMAPI SOAP endpoint controller.
/// </summary>
[ApiController]
[Route("sonos")]
public class SmapiController : ControllerBase
{
    private readonly SonosService _sonosService;
    private readonly ILogger<SmapiController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmapiController"/> class.
    /// </summary>
    /// <param name="sonosService">Sonos service.</param>
    /// <param name="logger">Logger.</param>
    public SmapiController(
        SonosService sonosService,
        ILogger<SmapiController> logger)
    {
        _sonosService = sonosService;
        _logger = logger;
    }

    /// <summary>
    /// SOAP endpoint for SMAPI requests.
    /// </summary>
    /// <returns>SOAP response.</returns>
    [HttpPost("smapi")]
    [AllowAnonymous]
    [Consumes("text/xml", "application/xml")]
    [Produces("text/xml", "application/xml")]
    public async Task<IActionResult> Smapi()
    {
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var soapRequest = await reader.ReadToEndAsync();
            
            _logger.LogDebug("SMAPI SOAP Request: {Request}", soapRequest);

            // Parse the SOAP request
            var doc = new XmlDocument();
            doc.LoadXml(soapRequest);

            var nsmgr = new XmlNamespaceManager(doc.NameTable);
            nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
            nsmgr.AddNamespace("ns", "http://www.sonos.com/Services/1.1");

            // Prefer HTTP Authorization bearer over SOAP headers for OAuth flows
            var authToken = ExtractBearerToken() ?? ExtractAuthToken(doc, nsmgr);
            if (string.IsNullOrWhiteSpace(authToken))
            {
                _logger.LogDebug("No auth token found in request");
            }
            else
            {
                _logger.LogDebug("Auth token found (length: {Length})", authToken.Length);
                // Log decoded payload (no signature) to confirm userId/exp
                var payload = DecodeTokenPayload(authToken);
                if (!string.IsNullOrEmpty(payload))
                {
                    var logged = payload.Length > 500 ? payload.Substring(0, 500) + "...<truncated>" : payload;
                    _logger.LogTrace("Auth token payload: {Payload}", logged);
                }
            }

            // Log SOAP header (trimmed) to see what Sonos sends
            var headerNode = doc.SelectSingleNode("//soap:Header", nsmgr);
            if (headerNode != null)
            {
                var headerXml = headerNode.OuterXml;
                const int maxHeaderLog = 1200;
                if (headerXml.Length > maxHeaderLog)
                {
                    headerXml = headerXml.Substring(0, maxHeaderLog) + "...<truncated>";
                }
                _logger.LogTrace("SOAP Header: {HeaderXml}", headerXml);
            }

            // Extract the method name
            var bodyNode = doc.SelectSingleNode("//soap:Body", nsmgr);
            if (bodyNode == null || bodyNode.FirstChild == null)
            {
                return BadRequest("Invalid SOAP request");
            }

            var methodName = bodyNode.FirstChild.LocalName;
            _logger.LogInformation("SMAPI method called: {Method}", methodName);

            string response;
            switch (methodName)
            {
                case "getLastUpdate":
                    // Basic implementation returning stable tokens and a poll interval
                    response = BuildSoapResponse("getLastUpdateResponse", SerializeGetLastUpdate());
                    break;
                case "getAppLink":
                    var householdId = GetElementValue(doc, "householdId", nsmgr);
                    var appLinkResult = _sonosService.GetAppLink(householdId);
                    response = BuildSoapResponse("getAppLinkResponse", SerializeGetAppLink(appLinkResult));
                    break;

                case "getDeviceAuthToken":
                    var linkCode = GetElementValue(doc, "linkCode", nsmgr);
                    try
                    {
                        var deviceAuthResult = _sonosService.GetDeviceAuthToken(linkCode);
                        response = BuildSoapResponse("getDeviceAuthTokenResponse", SerializeGetDeviceAuthToken(deviceAuthResult));
                    }
                    catch (Services.SoapFaultException soapEx)
                    {
                        response = BuildSoapFault(soapEx.FaultCode, soapEx.FaultString, soapEx.ExceptionInfo, soapEx.SonosError);
                    }
                    break;

                case "getMetadata":
                    var id = GetElementValue(doc, "id", nsmgr) ?? "root";
                    var index = int.Parse(GetElementValue(doc, "index", nsmgr) ?? "0");
                    var count = int.Parse(GetElementValue(doc, "count", nsmgr) ?? "100");
                    var recursiveText = GetElementValue(doc, "recursive", nsmgr);
                    var recursive = bool.TryParse(recursiveText, out var parsedRecursive) ? parsedRecursive : false;
                    _logger.LogInformation("getMetadata inputs: id={Id}, index={Index}, count={Count}, recursive={Recursive}, authTokenLen={AuthLen}", id, index, count, recursive, authToken?.Length ?? 0);
                    var metadataResult = _sonosService.GetMetadata(id, index, count, recursive, authToken);
                    response = BuildSoapResponse("getMetadataResponse", SerializeGetMetadata(metadataResult));
                    break;

                case "getExtendedMetadata":
                    // Minimal empty response to satisfy Sonos calls; extend as needed
                    response = BuildSoapResponse("getExtendedMetadataResponse", "<ns:getExtendedMetadataResult></ns:getExtendedMetadataResult>");
                    break;

                case "getMediaMetadata":
                    var trackId = GetElementValue(doc, "id", nsmgr);
                    var mediaMetadataResult = _sonosService.GetMediaMetadata(trackId, authToken);
                    response = BuildSoapResponse("getMediaMetadataResponse", SerializeMediaMetadata(mediaMetadataResult));
                    break;

                case "getMediaURI":
                    var mediaId = GetElementValue(doc, "id", nsmgr);
                    var uriResult = _sonosService.GetMediaURI(mediaId, authToken);
                    response = BuildSoapResponse("getMediaURIResponse", SerializeMediaURI(uriResult));
                    break;

                case "search":
                    var searchId = GetElementValue(doc, "id", nsmgr);
                    var term = GetElementValue(doc, "term", nsmgr);
                    var searchIndex = int.Parse(GetElementValue(doc, "index", nsmgr) ?? "0");
                    var searchCount = int.Parse(GetElementValue(doc, "count", nsmgr) ?? "100");
                    var searchResult = _sonosService.Search(searchId, term, searchIndex, searchCount, authToken);
                    response = BuildSoapResponse("searchResponse", SerializeSearch(searchResult));
                    break;

                case "reportAccountAction":
                    var actionType = GetElementValue(doc, "type", nsmgr);
                    _sonosService.ReportAccountAction(actionType);
                    response = BuildSoapResponse("reportAccountActionResponse", string.Empty);
                    break;

                default:
                    _logger.LogWarning("Unsupported SMAPI method: {Method}", methodName);
                    return BadRequest($"Unsupported method: {methodName}");
            }

            _logger.LogDebug("SMAPI SOAP Response: {Response}", response);
            return Content(response, "text/xml");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing SMAPI request");
            return StatusCode(500, BuildSoapFault("Server", ex.Message));
        }
    }

    private static string GetElementValue(XmlDocument doc, string elementName, XmlNamespaceManager nsmgr)
    {
        var node = doc.SelectSingleNode($"//ns:{elementName}", nsmgr);
        return node?.InnerText ?? string.Empty;
    }

    private static string? ExtractAuthToken(XmlDocument doc, XmlNamespaceManager nsmgr)
    {
        // Try to extract credentials from SOAP header
        var headerNode = doc.SelectSingleNode("//soap:Header", nsmgr);
        if (headerNode == null)
        {
            return null;
        }

        nsmgr.AddNamespace("cred", "http://www.sonos.com/Services/1.1");
        
        // Look for credentials/loginToken/token
        var tokenNode = headerNode.SelectSingleNode("//cred:credentials/cred:loginToken/cred:token", nsmgr);
        if (tokenNode != null)
        {
            return tokenNode.InnerText;
        }

        // Alternative: look for authToken directly
        tokenNode = headerNode.SelectSingleNode("//cred:authToken", nsmgr);
        return tokenNode?.InnerText;
    }

    private string? ExtractBearerToken()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values))
        {
            return null;
        }

        var header = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        const string bearerPrefix = "Bearer ";
        return header.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? header.Substring(bearerPrefix.Length).Trim()
            : null;
    }

    private static string? DecodeTokenPayload(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 1)
        {
            return null;
        }

        try
        {
            var payloadBytes = Base64UrlDecode(parts[0]);
            return System.Text.Encoding.UTF8.GetString(payloadBytes);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var normalized = input.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2:
                normalized += "==";
                break;
            case 3:
                normalized += "=";
                break;
        }

        return Convert.FromBase64String(normalized);
    }

    private static string BuildSoapResponse(string methodName, string body)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ns=""http://www.sonos.com/Services/1.1"">
    <soap:Body>
        <ns:{methodName}>
            {body}
        </ns:{methodName}>
    </soap:Body>
</soap:Envelope>";
    }

    private static string BuildSoapFault(string faultCode, string faultString)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
    <soap:Body>
        <soap:Fault>
            <faultcode>soap:{faultCode}</faultcode>
            <faultstring>{System.Security.SecurityElement.Escape(faultString)}</faultstring>
        </soap:Fault>
    </soap:Body>
</soap:Envelope>";
    }

    private static string BuildSoapFault(string faultCode, string faultString, string exceptionInfo, int sonosError)
    {
        return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ns=""http://www.sonos.com/Services/1.1"">
    <soap:Body>
        <soap:Fault>
            <faultcode>{System.Security.SecurityElement.Escape(faultCode)}</faultcode>
            <faultstring>{System.Security.SecurityElement.Escape(faultString)}</faultstring>
            <detail>
                <ns:ExceptionInfo>{System.Security.SecurityElement.Escape(exceptionInfo)}</ns:ExceptionInfo>
                <ns:SonosError>{sonosError}</ns:SonosError>
            </detail>
        </soap:Fault>
    </soap:Body>
</soap:Envelope>";
    }

    private static string SerializeGetLastUpdate()
    {
        // Return stable tokens; update these when catalog or user favorites change
        var favoritesToken = "favorites-1";
        var catalogToken = "catalog-1";
        var pollIntervalSeconds = 120;

        var sb = new StringBuilder();
        sb.AppendLine("<ns:getLastUpdateResult>");
        sb.AppendLine($"    <ns:favorites>{System.Security.SecurityElement.Escape(favoritesToken)}</ns:favorites>");
        sb.AppendLine($"    <ns:catalog>{System.Security.SecurityElement.Escape(catalogToken)}</ns:catalog>");
        sb.AppendLine($"    <ns:pollInterval>{pollIntervalSeconds}</ns:pollInterval>");
        sb.AppendLine("</ns:getLastUpdateResult>");
        return sb.ToString();
    }

    private static string SerializeGetMetadata(GetMetadataResponse response)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<ns:getMetadataResult>");
        sb.AppendLine($"    <ns:index>{response.Index}</ns:index>");
        sb.AppendLine($"    <ns:count>{response.Count}</ns:count>");
        sb.AppendLine($"    <ns:total>{response.Total}</ns:total>");

        if (response.MediaCollection != null)
        {
            foreach (var collection in response.MediaCollection)
            {
                sb.AppendLine($"    <ns:mediaCollection>");
                sb.AppendLine($"        <ns:id>{System.Security.SecurityElement.Escape(collection.Id)}</ns:id>");
                sb.AppendLine($"        <ns:title>{System.Security.SecurityElement.Escape(collection.Title)}</ns:title>");
                sb.AppendLine($"        <ns:itemType>{collection.ItemType}</ns:itemType>");
                if (!string.IsNullOrEmpty(collection.Artist))
                {
                    sb.AppendLine($"        <ns:artist>{System.Security.SecurityElement.Escape(collection.Artist)}</ns:artist>");
                }
                if (!string.IsNullOrEmpty(collection.AlbumArtURI))
                {
                    sb.AppendLine($"        <ns:albumArtURI>{System.Security.SecurityElement.Escape(collection.AlbumArtURI)}</ns:albumArtURI>");
                }
                sb.AppendLine($"        <ns:canPlay>{collection.CanPlay.ToString().ToLower()}</ns:canPlay>");
                sb.AppendLine($"    </ns:mediaCollection>");
            }
        }

        if (response.MediaMetadata != null)
        {
            foreach (var meta in response.MediaMetadata)
            {
                sb.AppendLine($"    <ns:mediaMetadata>");
                sb.AppendLine($"        <ns:id>{System.Security.SecurityElement.Escape(meta.Id)}</ns:id>");
                sb.AppendLine($"        <ns:title>{System.Security.SecurityElement.Escape(meta.Title)}</ns:title>");
                sb.AppendLine($"        <ns:mimeType>{meta.MimeType}</ns:mimeType>");
                sb.AppendLine($"        <ns:itemType>{meta.ItemType}</ns:itemType>");
                if (meta.TrackNumber.HasValue)
                {
                    sb.AppendLine($"        <ns:trackNumber>{meta.TrackNumber.Value}</ns:trackNumber>");
                }
                if (!string.IsNullOrEmpty(meta.Artist))
                {
                    sb.AppendLine($"        <ns:artist>{System.Security.SecurityElement.Escape(meta.Artist)}</ns:artist>");
                }
                if (!string.IsNullOrEmpty(meta.Album))
                {
                    sb.AppendLine($"        <ns:album>{System.Security.SecurityElement.Escape(meta.Album)}</ns:album>");
                }
                if (!string.IsNullOrEmpty(meta.AlbumArtURI))
                {
                    sb.AppendLine($"        <ns:albumArtURI>{System.Security.SecurityElement.Escape(meta.AlbumArtURI)}</ns:albumArtURI>");
                }
                if (meta.Duration.HasValue)
                {
                    sb.AppendLine($"        <ns:duration>{meta.Duration.Value}</ns:duration>");
                }
                sb.AppendLine($"        <ns:canPlay>{meta.CanPlay.ToString().ToLower()}</ns:canPlay>");
                sb.AppendLine($"    </ns:mediaMetadata>");
            }
        }

        sb.AppendLine($"</ns:getMetadataResult>");
        return sb.ToString();
    }

    private static string SerializeMediaMetadata(GetMediaMetadataResponse response)
    {
        var metadata = response.MediaMetadata;
        if (metadata == null)
        {
            return "<ns:getMediaMetadataResult />";
        }

        return $@"<ns:getMediaMetadataResult>
            <ns:mediaMetadata>
                <ns:id>{System.Security.SecurityElement.Escape(metadata.Id)}</ns:id>
                <ns:title>{System.Security.SecurityElement.Escape(metadata.Title)}</ns:title>
                <ns:mimeType>{metadata.MimeType}</ns:mimeType>
                <ns:itemType>{metadata.ItemType}</ns:itemType>
            </ns:mediaMetadata>
        </ns:getMediaMetadataResult>";
    }

    private static string SerializeMediaURI(GetMediaURIResponse response)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<ns:getMediaURIResult>");
        sb.AppendLine($"    <ns:mediaUri>{System.Security.SecurityElement.Escape(response.MediaUri ?? string.Empty)}</ns:mediaUri>");

        if (response.HttpHeaders != null && response.HttpHeaders.Any())
        {
            sb.AppendLine("    <ns:httpHeaders>");
            foreach (var header in response.HttpHeaders)
            {
                sb.AppendLine("        <ns:httpHeader>");
                sb.AppendLine($"            <ns:header>{System.Security.SecurityElement.Escape(header.Header)}</ns:header>");
                sb.AppendLine($"            <ns:value>{System.Security.SecurityElement.Escape(header.Value)}</ns:value>");
                sb.AppendLine("        </ns:httpHeader>");
            }
            sb.AppendLine("    </ns:httpHeaders>");
        }

        sb.AppendLine("</ns:getMediaURIResult>");
        return sb.ToString();
    }

    private static string SerializeGetAppLink(GetAppLinkResponse response)
    {
        return $@"<ns:getAppLinkResult>
            <ns:authorizeAccount>
                <ns:appUrlStringId>{response.AuthorizeAccount?.AppUrlStringId}</ns:appUrlStringId>
                <ns:deviceLink>
                    <ns:regUrl>{System.Security.SecurityElement.Escape(response.AuthorizeAccount?.DeviceLink?.RegUrl ?? string.Empty)}</ns:regUrl>
                    <ns:linkCode>{System.Security.SecurityElement.Escape(response.AuthorizeAccount?.DeviceLink?.LinkCode ?? string.Empty)}</ns:linkCode>
                    <ns:showLinkCode>{response.AuthorizeAccount?.DeviceLink?.ShowLinkCode.ToString().ToLower()}</ns:showLinkCode>
                </ns:deviceLink>
            </ns:authorizeAccount>
        </ns:getAppLinkResult>";
    }

    private static string SerializeGetDeviceAuthToken(GetDeviceAuthTokenResponse response)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<ns:getDeviceAuthTokenResult>");
        sb.AppendLine($"    <ns:authToken>{System.Security.SecurityElement.Escape(response.AuthToken)}</ns:authToken>");
        if (!string.IsNullOrWhiteSpace(response.PrivateKey))
        {
            sb.AppendLine($"    <ns:privateKey>{System.Security.SecurityElement.Escape(response.PrivateKey)}</ns:privateKey>");
        }
        sb.AppendLine("    <ns:userInfo>");
        sb.AppendLine($"        <ns:nickname>{System.Security.SecurityElement.Escape(response.UserInfo?.Nickname ?? string.Empty)}</ns:nickname>");
        sb.AppendLine($"        <ns:userIdHashCode>{System.Security.SecurityElement.Escape(response.UserInfo?.UserIdHashCode ?? string.Empty)}</ns:userIdHashCode>");
        sb.AppendLine("    </ns:userInfo>");
        sb.AppendLine("</ns:getDeviceAuthTokenResult>");
        return sb.ToString();
    }

    private static string SerializeSearch(SearchResponse response)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<ns:searchResult>");
        sb.AppendLine($"    <ns:index>{response.Index}</ns:index>");
        sb.AppendLine($"    <ns:count>{response.Count}</ns:count>");
        sb.AppendLine($"    <ns:total>{response.Total}</ns:total>");

        if (response.MediaCollection != null)
        {
            foreach (var collection in response.MediaCollection)
            {
                sb.AppendLine($"    <ns:mediaCollection>");
                sb.AppendLine($"        <ns:id>{System.Security.SecurityElement.Escape(collection.Id)}</ns:id>");
                sb.AppendLine($"        <ns:title>{System.Security.SecurityElement.Escape(collection.Title)}</ns:title>");
                sb.AppendLine($"        <ns:itemType>{collection.ItemType}</ns:itemType>");
                if (!string.IsNullOrEmpty(collection.Artist))
                {
                    sb.AppendLine($"        <ns:artist>{System.Security.SecurityElement.Escape(collection.Artist)}</ns:artist>");
                }
                if (!string.IsNullOrEmpty(collection.AlbumArtURI))
                {
                    sb.AppendLine($"        <ns:albumArtURI>{System.Security.SecurityElement.Escape(collection.AlbumArtURI)}</ns:albumArtURI>");
                }
                sb.AppendLine($"        <ns:canPlay>{collection.CanPlay.ToString().ToLower()}</ns:canPlay>");
                sb.AppendLine($"    </ns:mediaCollection>");
            }
        }
        if (response.MediaMetadata != null)
        {
            foreach (var meta in response.MediaMetadata)
            {
                sb.AppendLine($"    <ns:mediaMetadata>");
                sb.AppendLine($"        <ns:id>{System.Security.SecurityElement.Escape(meta.Id)}</ns:id>");
                sb.AppendLine($"        <ns:title>{System.Security.SecurityElement.Escape(meta.Title)}</ns:title>");
                sb.AppendLine($"        <ns:mimeType>{meta.MimeType}</ns:mimeType>");
                sb.AppendLine($"        <ns:itemType>{meta.ItemType}</ns:itemType>");
                if (meta.TrackNumber.HasValue)
                {
                    sb.AppendLine($"        <ns:trackNumber>{meta.TrackNumber.Value}</ns:trackNumber>");
                }
                if (!string.IsNullOrEmpty(meta.Artist))
                {
                    sb.AppendLine($"        <ns:artist>{System.Security.SecurityElement.Escape(meta.Artist)}</ns:artist>");
                }
                if (!string.IsNullOrEmpty(meta.Album))
                {
                    sb.AppendLine($"        <ns:album>{System.Security.SecurityElement.Escape(meta.Album)}</ns:album>");
                }
                if (!string.IsNullOrEmpty(meta.AlbumArtURI))
                {
                    sb.AppendLine($"        <ns:albumArtURI>{System.Security.SecurityElement.Escape(meta.AlbumArtURI)}</ns:albumArtURI>");
                }
                if (meta.Duration.HasValue)
                {
                    sb.AppendLine($"        <ns:duration>{meta.Duration.Value}</ns:duration>");
                }
                sb.AppendLine($"        <ns:canPlay>{meta.CanPlay.ToString().ToLower()}</ns:canPlay>");
                sb.AppendLine($"    </ns:mediaMetadata>");
            }
        }
        sb.AppendLine($"</ns:searchResult>");
        return sb.ToString();
    }
}
