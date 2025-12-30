using System;
using System.IO;
using System.Linq;
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
    private readonly LinkCodeService _linkCodeService;
    private readonly ILogger<SonosController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SonosController"/> class.
    /// </summary>
    /// <param name="userManager">User manager.</param>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="oauthService">OAuth token service.</param>
    /// <param name="linkCodeService">Link code service.</param>
    /// <param name="logger">Logger.</param>
    public SonosController(
        IUserManager userManager,
        ILibraryManager libraryManager,
        OAuthService oauthService,
        LinkCodeService linkCodeService,
        ILogger<SonosController> logger)
    {
        _userManager = userManager;
        _libraryManager = libraryManager;
        _oauthService = oauthService;
        _linkCodeService = linkCodeService;
        _logger = logger;
    }

    /// <summary>
    /// Sonos AppLink login page - displays login form.
    /// </summary>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string linkCode)
    {
        if (string.IsNullOrWhiteSpace(linkCode) || !_linkCodeService.IsValid(linkCode))
        {
            _logger.LogWarning("Invalid linkCode: {LinkCode}", linkCode);
            return BadRequest("Invalid or expired link code.");
        }

        _logger.LogInformation("Login GET: linkCode={LinkCode}", linkCode);

        var html = $@"<!DOCTYPE html>
<html>
<head>
    <title>Jellyfin - Sonos Login</title>
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
        p {{ margin: 15px 0; }}
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
    </style>
</head>
<body>
    <h1>Link Sonos with Jellyfin</h1>
    <p>Sign in to your Jellyfin account to authorize Sonos.</p>
    <form method='post' action='/sonos/login'>
        <input type='hidden' name='linkCode' value='{System.Net.WebUtility.HtmlEncode(linkCode)}' />
        <input type='text' id='username' name='username' placeholder='Username' required />
        <input type='password' id='password' name='password' placeholder='Password' required />
        <button type='submit'>Sign In</button>
    </form>
</body>
</html>";

        return Content(html, "text/html");
    }

    /// <summary>
    /// Sonos AppLink login POST - handles credentials and associates with linkCode.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult LoginPost([FromForm] LoginForm form)
    {
        _logger.LogInformation("Login POST: username={Username}, linkCode={LinkCode}", form.Username, form.LinkCode);

        if (string.IsNullOrWhiteSpace(form.LinkCode) || !_linkCodeService.IsValid(form.LinkCode))
        {
            _logger.LogWarning("Invalid linkCode: {LinkCode}", form.LinkCode);
            return BadRequest("Invalid or expired link code.");
        }

        try
        {
            // Use reflection to avoid strong binding to Jellyfin.Data.Entities.User (prevents TypeLoadException)
            var getUserByName = _userManager.GetType().GetMethod("GetUserByName");
            var userObj = getUserByName?.Invoke(_userManager, new object[] { form.Username });

            if (userObj == null)
            {
                _logger.LogWarning("Login failed: user '{Username}' not found", form.Username);
                return StatusCode(403, RenderLoginError("Login failed!", "Invalid username or password."));
            }

            var idProp = userObj.GetType().GetProperty("Id");
            if (idProp == null)
            {
                _logger.LogError("User object missing Id property via reflection");
                return StatusCode(500, RenderLoginError("Error", "Unable to load user information."));
            }

            var userId = (Guid)idProp.GetValue(userObj)!;
            _logger.LogInformation("User found: {Username} (ID: {UserId})", form.Username, userId);

            // NOTE: Jellyfin plugins don't have a built-in password validator exposed.
            // In a production scenario, you'd validate the password. For now, we trust the username lookup.
            
            // Generate auth token for this user
            var tokenResult = _oauthService.IssueAccessToken(userId, form.Username, "smapi");
            
            // Associate the link code with this user's auth token
            var association = new LinkCodeAssociation
            {
                AuthToken = tokenResult.Token,
                UserId = userId,
                Username = form.Username,
                CreatedAt = DateTime.UtcNow
            };

            if (!_linkCodeService.Associate(form.LinkCode, association))
            {
                _logger.LogError("Failed to associate link code with user");
                return StatusCode(500, "Failed to complete authorization.");
            }

            _logger.LogInformation("Login successful: user={Username}, linkCode={LinkCode}", form.Username, form.LinkCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login processing for user={Username}", form.Username);
            return StatusCode(500, RenderLoginError("Error", "An error occurred during login. Please try again."));
        }

        var successHtml = @"<!DOCTYPE html>
<html>
<head>
    <title>Success</title>
    <style>
        body {
            font-family: Arial, sans-serif;
            max-width: 500px;
            margin: 50px auto;
            padding: 20px;
            background-color: #0b0b0b;
            color: #ffffff;
            text-align: center;
        }
        h1 { color: #00a4dc; }
        .success { 
            background-color: #1a3a1a; 
            color: #6bff6b; 
            padding: 15px;
            border-radius: 5px;
            margin: 20px 0;
        }
    </style>
</head>
<body>
    <h1>Login Successful!</h1>
    <div class='success'>
        <p>Your Jellyfin account is now linked with Sonos.</p>
        <p>Please return to the Sonos app to complete setup.</p>
    </div>
</body>
</html>";

        return Content(successHtml, "text/html");
    }

    private string RenderLoginError(string title, string message)
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <title>{System.Net.WebUtility.HtmlEncode(title)}</title>
    <style>
        body {{
            font-family: Arial, sans-serif;
            max-width: 500px;
            margin: 50px auto;
            padding: 20px;
            background-color: #0b0b0b;
            color: #ffffff;
        }}
        h1 {{ color: #ff6b6b; }}
        .error {{
            background-color: #5c1a1a;
            color: #ff6b6b;
            padding: 15px;
            border-radius: 5px;
            margin: 20px 0;
        }}
    </style>
</head>
<body>
    <h1>{System.Net.WebUtility.HtmlEncode(title)}</h1>
    <div class='error'>{System.Net.WebUtility.HtmlEncode(message)}</div>
</body>
</html>";
    }

    /// <summary>
    /// OAuth authorization endpoint (Authorization Code + optional PKCE).
    /// </summary>
    [HttpGet("oauth/authorize")]
    [AllowAnonymous]
    public IActionResult Authorize([FromQuery] OAuthAuthorizeQuery query)
    {
        // Log the raw query string to see what Sonos is actually sending
        var rawQuery = HttpContext.Request.QueryString.Value;
        _logger.LogInformation("OAuth authorize GET - Raw query string: {RawQuery}", rawQuery);
        _logger.LogInformation("OAuth authorize GET: responseType={ResponseType}, clientId={ClientId}, redirectUri={RedirectUri}, scope={Scope}, state={State}",
            query.ResponseType, query.ClientId, query.RedirectUri, query.Scope, query.State);

        if (!string.Equals(query.ResponseType, "code", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid response_type: {ResponseType}", query.ResponseType);
            return BadRequest("response_type must be 'code'.");
        }

        if (string.IsNullOrWhiteSpace(query.ClientId) || string.IsNullOrWhiteSpace(query.RedirectUri))
        {
            _logger.LogWarning("Missing required OAuth parameters: clientId={ClientId}, redirectUri={RedirectUri}", 
                string.IsNullOrWhiteSpace(query.ClientId) ? "missing" : "present",
                string.IsNullOrWhiteSpace(query.RedirectUri) ? "missing" : "present");
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
        _logger.LogInformation("OAuth authorize POST: username={Username}, clientId={ClientId}, redirectUri={RedirectUri}",
            form.Username, form.ClientId, form.RedirectUri);

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
            _logger.LogWarning("OAuth authorization failed: user '{Username}' not found", form.Username);
            return Unauthorized("Invalid username or password.");
        }

        _logger.LogInformation("User found: {Username} (ID: {UserId})", form.Username, user.Id);

        if (string.IsNullOrWhiteSpace(form.Password))
        {
            return BadRequest("Password is required.");
        }

        // NOTE: Jellyfin plugins do not currently expose a password verifier; we rely on the presence of the account and password being provided.
        var code = _oauthService.CreateAuthorizationCode(user.Id, form.Username, form.ClientId, form.RedirectUri, form.CodeChallenge, form.CodeChallengeMethod);
        _logger.LogInformation("Authorization code issued: code={Code}, user={Username}", code?.Substring(0, 8) + "...", form.Username);

        if (string.IsNullOrEmpty(code))
        {
            _logger.LogError("Failed to create authorization code");
            return StatusCode(500, "Failed to create authorization code");
        }

        var redirectUri = BuildRedirectUri(form.RedirectUri, code, form.State);
        _logger.LogInformation("Redirecting to: {RedirectUri}", redirectUri);
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
        _logger.LogInformation("Token request: grantType={GrantType}, code={Code}, clientId={ClientId}",
            request.GrantType, string.IsNullOrEmpty(request.Code) ? "empty" : "present", request.ClientId);

        var scope = string.IsNullOrWhiteSpace(request.Scope) ? "smapi" : request.Scope;

        if (string.Equals(request.GrantType, "authorization_code", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.RedirectUri))
            {
                return BadRequest(new { error = "invalid_request", error_description = "code, client_id, and redirect_uri are required." });
            }

            if (!_oauthService.TryRedeemCode(request.Code, request.ClientId, request.RedirectUri, request.CodeVerifier, out var authCode) || authCode == null)
            {
                _logger.LogWarning("Failed to redeem authorization code");
                return Unauthorized(new { error = "invalid_grant", error_description = "Authorization code is invalid or expired." });
            }

            _logger.LogInformation("Authorization code redeemed for user: {Username}", authCode.Username);

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
                _logger.LogWarning("Failed to exchange refresh token");
                return Unauthorized(new { error = "invalid_grant", error_description = "Refresh token is invalid or expired." });
            }

            _logger.LogInformation("Refresh token exchanged successfully");

            return Ok(new
            {
                token_type = "Bearer",
                access_token = accessToken.Token,
                expires_in = (int)(accessToken.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds,
                refresh_token = refreshToken.Token,
                scope = accessToken.Scope
            });
        }

        _logger.LogWarning("Unsupported grant type: {GrantType}", request.GrantType);
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
    [HttpGet("stream")]
    [AllowAnonymous]
    public async Task<IActionResult> Stream([FromQuery] string trackId)
    {
        try
        {
            _logger.LogInformation("Stream request for track: {TrackId}", trackId);

            if (string.IsNullOrWhiteSpace(trackId))
            {
                return BadRequest("Track ID is required");
            }

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

            // Disable response compression to avoid Content-Length mismatches
            // and ensure Sonos can seek properly
            Response.Headers["Content-Encoding"] = "identity";
            Response.Headers["Accept-Ranges"] = "bytes";

            // Open file stream
            var stream = new FileStream(audioItem.Path, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Return file stream
            _logger.LogInformation("Streaming track: {TrackName} ({Path}) for user {User} [Mime: {MimeType}]", 
                audioItem.Name, audioItem.Path, principal.Username, mimeType);
            return File(stream, mimeType, enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming track");
            return StatusCode(500, "Error streaming track");
        }
    }

    /// <summary>
    /// Debug endpoint to check current plugin configuration (requires admin authorization).
    /// </summary>
    [HttpGet("debug/config")]
    [Authorize]
    public IActionResult DebugConfig()
    {
        var plugin = Plugin.Instance;
        var config = plugin?.Configuration;

        _logger.LogInformation("Debug config requested: Plugin exists={PluginExists}, Config exists={ConfigExists}, ExternalUrl={ExternalUrl}",
            plugin != null ? "yes" : "no",
            config != null ? "yes" : "no",
            config?.ExternalUrl ?? "null");

        if (plugin == null)
        {
            return BadRequest(new { error = "Plugin instance not found" });
        }

        if (config == null)
        {
            return BadRequest(new { error = "Configuration not found" });
        }

        var response = new ConfigDebugResponse
        {
            ExternalUrl = config.ExternalUrl,
            ServiceName = config.ServiceName,
            ServiceId = config.ServiceId,
            HasSecretKey = !string.IsNullOrWhiteSpace(config.SecretKey),
            PluginInstanceExists = "yes"
        };

        _logger.LogInformation("Debug config response: {Response}", response);
        return Ok(response);
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
            ".mp4" => "audio/mp4",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            ".oga" => "audio/ogg",
            ".wav" => "audio/wav",
            ".wma" => "audio/x-ms-wma",
            ".asf" => "audio/x-ms-wma",
            ".aiff" => "audio/aiff",
            ".aif" => "audio/aiff",
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
        // 1) Custom X-Sonos-Auth header (preferred to avoid Jellyfin collision)
        if (Request.Headers.TryGetValue("X-Sonos-Auth", out var customValues))
        {
            var header = customValues.ToString();
            const string bearer = "Bearer ";
            if (header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            {
                return header.Substring(bearer.Length).Trim();
            }
        }

        // 2) Authorization header (fallback)
        if (Request.Headers.TryGetValue("Authorization", out var values))
        {
            var header = values.ToString();
            const string bearer = "Bearer ";
            if (header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            {
                return header.Substring(bearer.Length).Trim();
            }
        }

        // 3) sonos_token query string (preferred)
        if (Request.Query.TryGetValue("sonos_token", out var sonosTokenValues))
        {
            return sonosTokenValues.ToString();
        }

        // 4) access_token query string (legacy/fallback)
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
/// Sonos AppLink login form.
/// </summary>
public class LoginForm
{
    public string LinkCode { get; set; } = string.Empty;
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

/// <summary>
/// Debug endpoint to check current plugin configuration.
/// </summary>
public class ConfigDebugResponse
{
    public string? ExternalUrl { get; set; }
    public string? ServiceName { get; set; }
    public int ServiceId { get; set; }
    public bool HasSecretKey { get; set; }
    public string? PluginInstanceExists { get; set; }
}
#pragma warning restore CS1591
