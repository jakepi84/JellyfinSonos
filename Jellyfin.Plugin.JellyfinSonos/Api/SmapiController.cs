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
                case "getAppLink":
                    var householdId = GetElementValue(doc, "householdId", nsmgr);
                    var appLinkResult = _sonosService.GetAppLink(householdId);
                    response = BuildSoapResponse("getAppLinkResponse", SerializeGetAppLink(appLinkResult));
                    break;

                case "getDeviceAuthToken":
                    response = BuildSoapFault("Client", "OAuth is used for this service. Tokens are issued via /sonos/oauth/token.");
                    break;

                case "getMetadata":
                    var id = GetElementValue(doc, "id", nsmgr) ?? "root";
                    var index = int.Parse(GetElementValue(doc, "index", nsmgr) ?? "0");
                    var count = int.Parse(GetElementValue(doc, "count", nsmgr) ?? "100");
                    var recursive = bool.Parse(GetElementValue(doc, "recursive", nsmgr) ?? "false");
                    var metadataResult = _sonosService.GetMetadata(id, index, count, recursive, authToken);
                    response = BuildSoapResponse("getMetadataResponse", SerializeGetMetadata(metadataResult));
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

    private static string SerializeSearch(SearchResponse response)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<ns:searchResult>");
        sb.AppendLine($"    <ns:index>{response.Index}</ns:index>");
        sb.AppendLine($"    <ns:count>{response.Count}</ns:count>");
        sb.AppendLine($"    <ns:total>{response.Total}</ns:total>");
        sb.AppendLine($"</ns:searchResult>");
        return sb.ToString();
    }
}
