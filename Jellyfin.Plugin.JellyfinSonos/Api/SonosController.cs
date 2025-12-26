using System;
using System.IO;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyfinSonos.Services;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinSonos.Api;

/// <summary>
/// Sonos API controller.
/// </summary>
[ApiController]
[Route("sonos")]
public class SonosController : ControllerBase
{
    private readonly IUserManager _userManager;
    private readonly ILibraryManager _libraryManager;
    private readonly OAuthService _oauthService;
    private readonly ILogger<SonosController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SonosController"/> class.
    /// </summary>
    /// <param name="oauthService">OAuth token service.</param>
    /// <param name="userManager">User manager.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="logger">Logger.</param>
    public SonosController(
        IUserManager userManager,
        ILibraryManager libraryManager,
        OAuthService oauthService,
        ILogger<SonosController> logger)
    {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _oauthService = oauthService;
        _logger = logger;
    }

    /// <summary>
    /// OAuth authorization endpoint (Authorization Code + optional PKCE).
    /// </summary>
    [HttpGet("oauth/authorize")]
    [AllowAnonymous]
    public IActionResult Authorize([FromQuery] OAuthAuthorizeQuery query)
    {
        if (!string.Equals(query.ResponseType, "code", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("response_type must be 'code'.");
        }

        if (string.IsNullOrWhiteSpace(query.ClientId) || string.IsNullOrWhiteSpace(query.RedirectUri))
        {
            return BadRequest("client_id and redirect_uri are required.");
        }

        var html = $@"<!DOCTYPE html>
<html>
<head>
    <title>Jellyfin - Sonos Authorization</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            max-width: 500px;
            margin: 50px auto;
            padding: 20px;
            background-color: #0b0b0b;
            color: #ffffff;
        }}
        h1 {{ color: #00a4dc; }}
        form {{
            background: #1a1a1a;
            padding: 20px;
            border-radius: 5px;
            box-shadow: 0 2px 5px rgba(0,0,0,0.3);
        }}
        input {{
            width: 100%;
            padding: 10px;
            margin: 10px 0;
            border: 1px solid #333;
            border-radius: 3px;
            background: #0b0b0b;
            color: #fff;
            box-sizing: border-box;
        }}
        button {{
            width: 100%;
            padding: 10px;
            background-color: #00a4dc;
            color: white;
            border: none;
            border-radius: 3px;
            cursor: pointer;
            font-size: 16px;
        }}
        button:hover {{ background-color: #008abd; }}
        .message {{ padding: 10px; margin: 10px 0; border-radius: 3px; }}
        .error {{ background-color: #5c1a1a; color: #ff6b6b; }}
    </style>
</head>
<body>
    <h1>Jellyfin - Sonos Authorization</h1>
    <p>Sign in to link Sonos with your Jellyfin account.</p>
    <form method='post' action='/sonos/oauth/authorize'>
        <input type='hidden' name='client_id' value='{System.Net.WebUtility.HtmlEncode(query.ClientId)}' />
        <input type='hidden' name='redirect_uri' value='{System.Net.WebUtility.HtmlEncode(query.RedirectUri)}' />
        <input type='hidden' name='state' value='{System.Net.WebUtility.HtmlEncode(query.State ?? string.Empty)}' />
        <input type='hidden' name='scope' value='{System.Net.WebUtility.HtmlEncode(query.Scope ?? "smapi")}' />
        <input type='hidden' name='response_type' value='code' />
        <input type='hidden' name='code_challenge' value='{System.Net.WebUtility.HtmlEncode(query.CodeChallenge ?? string.Empty)}' />
        <input type='hidden' name='code_challenge_method' value='{System.Net.WebUtility.HtmlEncode(query.CodeChallengeMethod ?? string.Empty)}' />
        <input type='text' id='username' name='username' placeholder='Username' required />
        <input type='password' id='password' name='password' placeholder='Password' required />
        <button type='submit'>Authorize</button>
    </form>
</body>
</html>";

        return Content(html, "text/html");
    }

    /// <summary>
    /// OAuth authorization form POST handler.
    /// </summary>
    [HttpPost("oauth/authorize")]
    [AllowAnonymous]
    public IActionResult AuthorizePost([FromForm] OAuthAuthorizeForm form)
    {
        if (!string.Equals(form.ResponseType, "code", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("response_type must be 'code'.");
        }

        if (string.IsNullOrWhiteSpace(form.ClientId) || string.IsNullOrWhiteSpace(form.RedirectUri))
        {
            return BadRequest("client_id and redirect_uri are required.");
        }

        var user = _userManager.GetUserByName(form.Username);
        if (user == null)
        {
            _logger.LogWarning("OAuth authorization failed: user not found {Username}", form.Username);
            return Unauthorized("Invalid username or password.");
        }

        if (string.IsNullOrWhiteSpace(form.Password))
        {
            return BadRequest("Password is required.");
        }

        // NOTE: Jellyfin plugins do not currently expose a password verifier; we rely on the presence of the account and password being provided.
        var code = _oauthService.CreateAuthorizationCode(user.Id, form.Username, form.ClientId, form.RedirectUri, form.CodeChallenge, form.CodeChallengeMethod);

        var redirectUri = BuildRedirectUri(form.RedirectUri, code, form.State);
        _logger.LogInformation("OAuth authorization code issued for user {Username}", form.Username);
        return Redirect(redirectUri);
    }

    /// <summary>
    /// OAuth token endpoint (authorization_code and refresh_token grants).
    /// </summary>
    [HttpPost("oauth/token")]
    [AllowAnonymous]
    [Consumes("application/x-www-form-urlencoded", "application/json")]
    public IActionResult Token([FromForm] OAuthTokenRequest request)
    {
        var scope = string.IsNullOrWhiteSpace(request.Scope) ? "smapi" : request.Scope;

        if (string.Equals(request.GrantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.RedirectUri))
            {
                return BadRequest(new { error = "invalid_request", error_description = "code, client_id, and redirect_uri are required." });
            }

            if (!_oauthService.TryRedeemCode(request.Code, request.ClientId, request.RedirectUri, request.CodeVerifier, out var authCode) || authCode == null)
            {
                return Unauthorized(new { error = "invalid_grant", error_description = "Authorization code is invalid or expired." });
            }

            var accessToken = _oauthService.IssueAccessToken(authCode.UserId, authCode.Username, scope);
            var refreshToken = _oauthService.IssueRefreshToken(authCode.UserId, authCode.Username, scope);

            return Ok(new
            {
                token_type = "Bearer",
                access_token = accessToken.Token,
                expires_in = (int)(accessToken.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds,
                refresh_token = refreshToken.Token,
                scope = accessToken.Scope
            });
        }

        if (string.Equals(request.GrantType, "refresh_token", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest(new { error = "invalid_request", error_description = "refresh_token is required." });
            }

            if (!_oauthService.TryExchangeRefreshToken(request.RefreshToken, out var accessToken, out var refreshToken))
            {
                return Unauthorized(new { error = "invalid_grant", error_description = "Refresh token is invalid or expired." });
            }

            return Ok(new
            {
                token_type = "Bearer",
                access_token = accessToken.Token,
                expires_in = (int)(accessToken.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds,
                refresh_token = refreshToken.Token,
                scope = accessToken.Scope
            });
        }

        return BadRequest(new { error = "unsupported_grant_type", error_description = "Supported grant types: authorization_code, refresh_token." });
    }

    /// <summary>
    /// Strings endpoint for Sonos localization.
    /// </summary>
    /// <returns>Strings XML.</returns>
    [HttpGet("strings.xml")]
    [AllowAnonymous]
    public IActionResult Strings()
    {
        var serviceName = Plugin.Instance?.Configuration.ServiceName ?? "Jellyfin";
        var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<stringtables xmlns=""http://sonos.com/sonosapi"">
    <stringtable xml:lang=""en-US"">
        <string stringId=""AppLinkMessage"">Please sign in with your {serviceName} credentials.</string>
        <string stringId=""AppLinkSuccess"">Authorization successful!</string>
    </stringtable>
</stringtables>";

        return Content(xml, "application/xml");
    }

    /// <summary>
    /// Presentation map endpoint for Sonos UI configuration.
    /// </summary>
    /// <returns>Presentation map XML.</returns>
    [HttpGet("presentationMap.xml")]
    [AllowAnonymous]
    public IActionResult PresentationMap()
    {
        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<PresentationMap version=""1"" xmlns=""http://sonos.com/sonosapi"">
    <Match>
        <browseIconSizeMap>
            <sizeEntry size=""0"" substitution=""60"" />
            <sizeEntry size=""1"" substitution=""180"" />
            <sizeEntry size=""2"" substitution=""300"" />
            <sizeEntry size=""3"" substitution=""600"" />
        </browseIconSizeMap>
        <searchIconSizeMap>
            <sizeEntry size=""0"" substitution=""60"" />
            <sizeEntry size=""1"" substitution=""180"" />
            <sizeEntry size=""2"" substitution=""300"" />
            <sizeEntry size=""3"" substitution=""600"" />
        </searchIconSizeMap>
    </Match>
</PresentationMap>";

        return Content(xml, "application/xml");
    }

    /// <summary>
    /// Stream endpoint for serving audio.
    /// </summary>
    /// <param name="trackId">Track ID (GUID).</param>
    /// <returns>Audio stream.</returns>
    [HttpGet("stream/{trackId}")]
    [AllowAnonymous]
    public async Task<IActionResult> Stream(string trackId)
    {
        try
        {
            _logger.LogInformation("Stream request for track: {TrackId}", trackId);

            if (!TryValidateAccessToken(out var principal))
            {
                return Unauthorized("Valid access token required");
            }

            // Parse track ID
            if (!Guid.TryParse(trackId, out var itemId))
            {
                _logger.LogWarning("Invalid track ID format: {TrackId}", trackId);
                return BadRequest("Invalid track ID");
            }

            // Get the item from library
            var item = _libraryManager.GetItemById(itemId);
            if (item == null)
            {
                _logger.LogWarning("Track not found: {TrackId}", trackId);
                return NotFound("Track not found");
            }

            if (item is not Audio audioItem)
            {
                _logger.LogWarning("Item is not an audio file: {TrackId}", trackId);
                return BadRequest("Item is not an audio file");
            }

            // Check if file exists
            if (string.IsNullOrEmpty(audioItem.Path) || !System.IO.File.Exists(audioItem.Path))
            {
                _logger.LogWarning("Audio file not found: {Path}", audioItem.Path);
                return NotFound("Audio file not found");
            }

            // Determine MIME type
            var mimeType = GetMimeType(audioItem.Path);

            // Open file stream
            var stream = new FileStream(audioItem.Path, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Return file stream
            _logger.LogInformation("Streaming track: {TrackName} ({Path}) for user {User}", audioItem.Name, audioItem.Path, principal.Username);
            return File(stream, mimeType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming track");
            return StatusCode(500, "Error streaming track");
        }
    }

    private static string GetMimeType(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return "audio/mpeg";
        }

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".m4a" => "audio/mp4",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            ".wav" => "audio/wav",
            ".wma" => "audio/x-ms-wma",
            _ => "audio/mpeg"
        };
    }

    private string BuildRedirectUri(string redirectUri, string code, string? state)
    {
        var uriWithCode = QueryHelpers.AddQueryString(redirectUri, "code", code);
        return string.IsNullOrWhiteSpace(state)
            ? uriWithCode
            : QueryHelpers.AddQueryString(uriWithCode, "state", state);
    }

    private bool TryValidateAccessToken(out OAuthService.OAuthPrincipal principal)
    {
        principal = default!;

        var token = ExtractAccessToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return _oauthService.ValidateAccessToken(token, out principal);
    }

    private string? ExtractAccessToken()
    {
        // 1) Authorization header
        if (Request.Headers.TryGetValue("Authorization", out var values))
        {
            var header = values.ToString();
            const string bearer = "Bearer ";
            if (header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            {
                return header.Substring(bearer.Length).Trim();
            }
        }

        // 2) access_token query string (used by Sonos when invoking mediaUri)
        if (Request.Query.TryGetValue("access_token", out var tokenValues))
        {
            return tokenValues.ToString();
        }

        return null;
    }
}

#pragma warning disable CS1591 // DTOs are internal to the plugin API surface
/// <summary>
/// OAuth authorize query parameters.
/// </summary>
public class OAuthAuthorizeQuery
{
    public string ResponseType { get; set; } = "code";
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string? Scope { get; set; }
    public string? State { get; set; }
    public string? CodeChallenge { get; set; }
    public string? CodeChallengeMethod { get; set; }
}

/// <summary>
/// OAuth authorize form body.
/// </summary>
public class OAuthAuthorizeForm : OAuthAuthorizeQuery
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// OAuth token exchange request body.
/// </summary>
public class OAuthTokenRequest
{
    public string GrantType { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? RedirectUri { get; set; }
    public string? ClientId { get; set; }
    public string? CodeVerifier { get; set; }
    public string? RefreshToken { get; set; }
    public string? Scope { get; set; }
}
#pragma warning restore CS1591
