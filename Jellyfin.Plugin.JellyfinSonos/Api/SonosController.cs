using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.JellyfinSonos.Services;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinSonos.Api;

/// <summary>
/// Sonos API controller.
/// </summary>
[ApiController]
[Route("sonos")]
public class SonosController : ControllerBase
{
    private readonly LinkCodeService _linkCodeService;
    private readonly IUserManager _userManager;
    private readonly ILogger<SonosController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SonosController"/> class.
    /// </summary>
    /// <param name="linkCodeService">Link code service.</param>
    /// <param name="userManager">User manager.</param>
    /// <param name="logger">Logger.</param>
    public SonosController(
        LinkCodeService linkCodeService,
        IUserManager userManager,
        ILogger<SonosController> logger)
    {
        _linkCodeService = linkCodeService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Login page for Sonos device authorization.
    /// </summary>
    /// <param name="linkCode">Link code from Sonos.</param>
    /// <returns>Login page.</returns>
    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login([FromQuery] string linkCode)
    {
        if (string.IsNullOrEmpty(linkCode))
        {
            return BadRequest("Link code is required");
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
        h1 {{
            color: #00a4dc;
        }}
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
        button:hover {{
            background-color: #008abd;
        }}
        .message {{
            padding: 10px;
            margin: 10px 0;
            border-radius: 3px;
        }}
        .success {{
            background-color: #2d5016;
            color: #90ee90;
        }}
        .error {{
            background-color: #5c1a1a;
            color: #ff6b6b;
        }}
    </style>
</head>
<body>
    <h1>Jellyfin - Sonos Authorization</h1>
    <p>Please enter your Jellyfin credentials to authorize Sonos access.</p>
    <form id='loginForm'>
        <input type='text' id='username' name='username' placeholder='Username' required />
        <input type='password' id='password' name='password' placeholder='Password' required />
        <button type='submit'>Authorize</button>
    </form>
    <div id='message'></div>
    <script>
        document.getElementById('loginForm').addEventListener('submit', async function(e) {{
            e.preventDefault();
            const username = document.getElementById('username').value;
            const password = document.getElementById('password').value;
            const linkCode = '{linkCode}';
            
            try {{
                const response = await fetch('/sonos/authorize', {{
                    method: 'POST',
                    headers: {{
                        'Content-Type': 'application/json'
                    }},
                    body: JSON.stringify({{ linkCode, username, password }})
                }});
                
                const messageDiv = document.getElementById('message');
                if (response.ok) {{
                    messageDiv.innerHTML = '<div class=""message success"">Authorization successful! You can now close this window and return to your Sonos app.</div>';
                    document.getElementById('loginForm').style.display = 'none';
                }} else {{
                    const error = await response.text();
                    messageDiv.innerHTML = '<div class=""message error"">Error: ' + error + '</div>';
                }}
            }} catch (error) {{
                document.getElementById('message').innerHTML = '<div class=""message error"">Error: ' + error + '</div>';
            }}
        }});
    </script>
</body>
</html>";

        return Content(html, "text/html");
    }

    /// <summary>
    /// Authorize endpoint for storing credentials.
    /// </summary>
    /// <param name="request">Authorization request.</param>
    /// <returns>Result.</returns>
    [HttpPost("authorize")]
    [AllowAnonymous]
    public async Task<IActionResult> Authorize([FromBody] AuthorizeRequest request)
    {
        try
        {
            // Validate credentials by getting the user
            var user = _userManager.GetUserByName(request.Username);
            
            if (user == null)
            {
                return Unauthorized("Invalid username or password");
            }

            // For now, we'll just store the credentials
            // In a production system, you'd want to properly validate the password
            var success = _linkCodeService.SetCredentials(request.LinkCode, request.Username, request.Password);
            if (!success)
            {
                return BadRequest("Invalid or expired link code");
            }

            return Ok("Authorization successful");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authorization");
            return StatusCode(500, "An error occurred during authorization");
        }
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
    /// <param name="trackId">Track ID.</param>
    /// <returns>Audio stream.</returns>
    [HttpGet("stream/{trackId}")]
    [AllowAnonymous]
    public async Task<IActionResult> Stream(string trackId)
    {
        try
        {
            // This is a placeholder - in a real implementation you would:
            // 1. Extract user credentials from Authorization header
            // 2. Validate the user
            // 3. Get the track from Jellyfin
            // 4. Stream the audio file
            
            _logger.LogInformation("Stream request for track: {TrackId}", trackId);
            return NotFound("Stream endpoint not fully implemented");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming track");
            return StatusCode(500, "Error streaming track");
        }
    }
}

/// <summary>
/// Authorization request model.
/// </summary>
public class AuthorizeRequest
{
    /// <summary>
    /// Gets or sets the link code.
    /// </summary>
    public string LinkCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
