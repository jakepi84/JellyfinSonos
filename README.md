# Jellyfin Sonos Plugin

A Jellyfin plugin that integrates with Sonos speakers using the Sonos Music API (SMAPI). This plugin allows you to access your Jellyfin music library directly from the Sonos app without requiring local device discovery or auto-registration.

## Features

- **✅ Full SMAPI Integration**: Implements the Sonos Music API Protocol for seamless integration with Sonos devices
- **✅ No Auto-Discovery**: Designed for manual registration via the Sonos Developer Portal
- **✅ Secure Authentication**: Uses AES-encrypted tokens for secure credential management
- **✅ Browse Music Library**: Browse your complete Jellyfin music library by artists, albums, and tracks
- **✅ Search**: Search for artists, albums, and tracks from the Sonos app
- **✅ Direct Streaming**: Stream music directly from your Jellyfin server to Sonos devices with range support

## Requirements

- Jellyfin Server 10.10.0 or later
- .NET 9.0 (for building from source)
- A Sonos Developer Account (for registering the service)
- Your Jellyfin server must be accessible via a public URL (or at least accessible from your Sonos devices)

## Installation

### Option 1: Install from Release (Recommended)

1. Download the latest `jellyfin-sonos_X.X.X.zip` from the [Releases page](https://github.com/jakepi84/JellyfinSonos/releases)
2. Extract the ZIP file to get the `JellyfinSonos` folder
3. Copy the `JellyfinSonos` folder to your Jellyfin plugins directory:
   - **Linux**: `/var/lib/jellyfin/plugins/JellyfinSonos/`
   - **Windows**: `%APPDATA%\Jellyfin\Server\plugins\JellyfinSonos\`
   - **Docker**: Mount the plugin folder to `/config/plugins/JellyfinSonos/`
4. Restart your Jellyfin server
5. Navigate to **Dashboard** → **Plugins** to verify the plugin is loaded

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/jakepi84/JellyfinSonos.git
cd JellyfinSonos

# Build the plugin
dotnet build --configuration Release

# Copy to Jellyfin plugins directory
mkdir -p /var/lib/jellyfin/plugins/JellyfinSonos/
cp Jellyfin.Plugin.JellyfinSonos/bin/Release/net9.0/Jellyfin.Plugin.JellyfinSonos.dll /var/lib/jellyfin/plugins/JellyfinSonos/

# Restart Jellyfin
sudo systemctl restart jellyfin
```

### Option 3: Add to Jellyfin Plugin Repository

Add this repository URL to your Jellyfin plugin repositories:
```
https://raw.githubusercontent.com/jakepi84/JellyfinSonos/main/manifest.json
```

Then install from **Dashboard** → **Plugins** → **Catalog**

## Configuration

After installing the plugin, you need to configure it before it can be used:

### Step 1: Access Plugin Configuration

1. Open your Jellyfin web interface
2. Navigate to **Dashboard** → **Plugins**
3. Find **Jellyfin Sonos** in the list and click on it
4. Click the **Settings** button

### Step 2: Configure Plugin Settings

Fill in the following required fields:

#### 1. Service Name
- **What**: The name that will appear in your Sonos app
- **Default**: `Jellyfin`
- **Example**: `My Jellyfin Music`
- **Note**: Keep it simple and recognizable

#### 2. Service ID
- **What**: A unique identifier for your service in the Sonos ecosystem
- **Default**: `247`
- **Valid Range**: 200-65535
- **Important**: Must be unique across all SMAPI services you use. If you have multiple Sonos services, each needs a different ID.

#### 3. External URL
- **What**: The public URL where your Jellyfin server can be reached
- **Example**: `https://jellyfin.example.com` or `https://music.mydomain.com`
- **Critical**: This URL **MUST** be accessible from your Sonos devices
- **Requirements**:
  - Must be HTTPS (SSL/TLS certificate required)
  - Must include protocol (https://)
  - Do NOT include trailing slash
  - Must be reachable from your local network where Sonos devices are located

#### 4. Secret Key
- **What**: A secure random string used for encrypting user credentials
- **Length**: At least 32 characters recommended
- **Important**: Keep this secret! Don't share it or commit it to version control
- **Used for**: Signing OAuth access tokens and refresh tokens (no plaintext passwords are stored)

**Generate a secure key:**

```bash
# Linux/Mac
openssl rand -base64 32

# Windows PowerShell
$bytes = New-Object Byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
[Convert]::ToBase64String($bytes)

# Online (use a password generator)
# Generate a 32+ character random string at: https://passwordsgenerator.net/
```

### Step 3: Save Configuration

Click **Save** to store your settings. Note the SMAPI endpoint URL displayed on the page - you'll need this for Sonos registration.

## Registering with Sonos Developer Portal

**Important**: You must register your service through the Sonos Developer Portal. Auto-discovery is NOT supported per Sonos policy.

### Prerequisites

Before registering:
- ✅ Plugin installed and configured in Jellyfin
- ✅ Jellyfin accessible via public HTTPS URL
- ✅ Test accessibility: Open `https://your-server.com/sonos/strings.xml` (XML) and `https://your-server.com/sonos/oauth/authorize?client_id=247&response_type=code&redirect_uri=https://example.com/callback` (login page)

### Registration Process

#### Step 1: Create Sonos Developer Account

1. Go to [Sonos Developer Portal](https://developer.sonos.com/)
2. Click **Sign Up** or **Log In**
3. Complete account registration

#### Step 2: Register Your Service

1. Navigate to **My Services** or **Create New Service**
2. Click **Add New Service**
3. Choose **SMAPI** as the service type

#### Step 3: Fill Service Details

| Field | Value |
|-------|-------|
| **Service Name** | Same as configured in plugin (e.g., "Jellyfin") |
| **Service ID** | Same as configured in plugin (e.g., 247) |
| **Endpoint URL** | `https://your-server.com/sonos/smapi` |
| **Polling Interval** | 30 seconds (default) |
| **Authentication Method** | **OAuth** |
| **Authorization URL** | `https://your-server.com/sonos/oauth/authorize` |
| **Token URL** | `https://your-server.com/sonos/oauth/token` |
| **Refresh Token URL** | `https://your-server.com/sonos/oauth/token` (same endpoint) |
| **Strings URL** | `https://your-server.com/sonos/strings.xml` |
| **Presentation Map URL** | `https://your-server.com/sonos/presentationMap.xml` |
| **Container Type** | Music Service |
| **Secure URI** | Yes (HTTPS required) |

#### Step 4: Service Capabilities

Enable these capabilities:
- ✅ Browse
- ✅ Search  
- ✅ Playback
- ✅ User Content

#### Step 5: Submit for Review

1. Review all information carefully
2. Click **Submit for Approval**
3. Wait for Sonos to review your service (typically 1-5 business days)
4. You'll receive an email when approved or if changes are needed

### Alternative: Private/Development Mode

For personal use without approval:
1. Register as above but select **Development Mode**
2. You'll get a limited number of test devices (usually 5)
3. Service works immediately without approval
4. Suitable for home use

## Using the Plugin

### Initial Setup (First Time)

Once your service is approved by Sonos:

#### Step 1: Add Service to Sonos App

1. Open the **Sonos app** on your phone or tablet
2. Go to **Settings** → **Services & Voice** → **Music & Content**
3. Tap **Add a Service**
4. Scroll to find your service name (e.g., "Jellyfin")
5. Tap **Add to Sonos**

#### Step 2: Authorize Your Account

1. The app will show "Linking Sonos with [Your Service]"
2. Tap **Authorize**
3. Your browser will open with the Jellyfin login page
4. Enter your **Jellyfin username and password**
5. Click **Authorize**
6. You should see "Authorization successful!"
7. Return to the Sonos app

#### Step 3: Complete Setup

1. The Sonos app will confirm the service is added
2. You should now see your service in the **Browse** section
3. The service icon will show your configured service name

### Daily Use

#### Browse Music

1. Open Sonos app
2. Tap **Browse**
3. Select your Jellyfin service
4. Navigate through:
   - **Artists** - Browse by artist
   - **Albums** - Browse all albums
   - **Search** - Search for specific content

#### Play Music

1. Navigate to an album or artist
2. Tap on a track to play
3. Or tap **Play All** to play an entire album/artist
4. Use standard Sonos controls (play, pause, skip, volume)

#### Search for Music

1. In your Jellyfin service, tap **Search**
2. Choose search type:
   - **Artists** - Find artists by name
   - **Albums** - Find albums by title
   - **Tracks** - Find specific songs
3. Enter search term
4. Tap results to play

### Troubleshooting

#### Service Not Appearing in Sonos App

**Causes:**
- Service not approved by Sonos yet
- Service not properly registered
- Sonos app cache issue

**Solutions:**
1. Check Sonos Developer Portal for approval status
2. Try logging out and back into Sonos app
3. Restart Sonos app
4. Ensure you're on the same network as your Sonos devices

#### "Authorization Failed" Error

**Causes:**
- Incorrect Jellyfin credentials
- Authorization code expired (10 minute timeout) or refresh token expired
- External URL not accessible

**Solutions:**
1. Double-check your Jellyfin username and password
2. Try authorization again from the Sonos app (generates a new OAuth code)
3. Test external URL: Open `https://your-server.com/sonos/oauth/authorize?client_id=247&response_type=code&redirect_uri=https://example.com/callback` in a browser
4. Verify firewall allows HTTPS traffic to Jellyfin

#### "Cannot Browse" or Empty Lists

**Causes:**
- No music in Jellyfin library
- User doesn't have access to music libraries
- Authentication token expired

**Solutions:**
1. Ensure you have music imported in Jellyfin
2. Verify the user has permissions to access music libraries
3. Re-authorize: Settings → Services → Your Service → Reauthorize

#### Music Won't Play / "Track Not Available"

**Causes:**
- Audio file format not supported by Sonos
- File path issues
- Streaming URL not accessible

**Solutions:**
1. Check audio format (Sonos supports: MP3, FLAC, AAC, WAV, OGG)
2. Verify files exist on Jellyfin server
3. Check Jellyfin logs for errors
4. Test streaming URL manually

#### Slow Performance

**Causes:**
- Network latency
- Large music library
- Server performance

**Solutions:**
1. Ensure good network connection
2. Consider using wired connection for Jellyfin server
3. Check Jellyfin server resources (CPU, RAM)
4. Enable caching in Jellyfin if needed

### Re-Authorizing

If you change your Jellyfin password or need to refresh authorization:

1. Open Sonos app
2. Go to **Settings** → **Services & Voice**
3. Find your Jellyfin service
4. Tap on it
5. Select **Reauthorize Account**
6. Follow authorization steps again

## Advanced Configuration

### Using Behind Reverse Proxy

If using a reverse proxy (nginx, Caddy, Traefik), ensure:

```nginx
# nginx example
location /sonos {
    proxy_pass http://jellyfin:8096/sonos;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    
    # Important for streaming
    proxy_buffering off;
    proxy_request_buffering off;
}
```

### SSL/TLS Certificate

Sonos requires HTTPS. Options:
- **Let's Encrypt**: Free automated certificates
- **Cloudflare**: Free SSL with Cloudflare proxy
- **Self-signed**: Works but requires certificate import on devices

### Firewall Configuration

Open these ports:
- **443** (HTTPS) - For SMAPI endpoint
- **8096** (optional) - If accessing Jellyfin directly

### Multiple Users

To add multiple Jellyfin users to Sonos:
1. Complete initial setup for first user
2. In Sonos app, go to your service settings
3. Look for "Add Another Account" or similar
4. Follow authorization flow with different Jellyfin credentials
5. Sonos will show a dropdown to switch between users

## Security Considerations

### Best Practices

1. **Use HTTPS**: Never expose plugin without SSL/TLS
2. **Strong Secret Key**: Use 32+ character random string
3. **Unique Service ID**: Don't reuse IDs from other services
4. **Regular Updates**: Keep Jellyfin and plugin updated
5. **Monitor Access**: Check Jellyfin logs for unusual activity

### Network Security

- Use firewall rules to limit access
- Consider VPN for external access
- Enable Jellyfin's built-in auth features
- Use strong passwords for Jellyfin users

## Supported Audio Formats

The plugin streams audio files directly without transcoding. Sonos supports:

| Format | Extension | Supported |
|--------|-----------|-----------|
| MP3 | .mp3 | ✅ Yes |
| FLAC | .flac | ✅ Yes (16/24-bit, up to 48kHz for S1, 192kHz for S2) |
| AAC | .m4a, .aac | ✅ Yes |
| WAV | .wav | ✅ Yes |
| OGG Vorbis | .ogg | ✅ Yes |
| WMA | .wma | ✅ Yes (Not WMA Lossless/Pro) |

**Note**: For files not natively supported, consider transcoding in Jellyfin before streaming.

## Registering with Sonos Developer Portal

1. Open the Sonos app on your mobile device or desktop
2. Go to **Settings** → **Services & Voice** → **Music & Content**
3. Tap **Add a Service**
4. Find your service name (e.g., "Jellyfin") in the list
5. Tap **Add to Sonos**
6. You'll be redirected to a login page - enter your Jellyfin credentials
7. Once authorized, you can browse and play your Jellyfin music library from the Sonos app

**Note**: Duplicate section removed - see detailed instructions above.

## Architecture

This plugin implements:

- **SMAPI SOAP Service**: Handles Sonos API requests for browsing and playback
- **Authentication Flow**: OAuth 2.0 Authorization Code with refresh tokens (PKCE)
- **REST API Endpoints**: Provides OAuth authorize/token, streaming, and configuration endpoints
- **Jellyfin Integration**: Integrates with Jellyfin's library manager to access music content

### Key Endpoints

- `/sonos/oauth/authorize` - OAuth authorization page (Authorization Code + PKCE)
- `/sonos/oauth/token` - OAuth token endpoint (authorization_code & refresh_token)
- `/sonos/smapi` - SOAP endpoint for Sonos API calls (getMetadata, getMediaURI, search, etc.)
- `/sonos/stream/{trackId}` - Audio streaming endpoint with range request support
- `/sonos/strings.xml` - Localization strings for Sonos UI
- `/sonos/presentationMap.xml` - UI configuration and icon sizes

### Technical Details

- **Language**: C# / .NET 9.0
- **Plugin System**: Jellyfin Plugin Framework
- **Authentication**: HMAC-signed OAuth access tokens (secret key configured in plugin)
- **Streaming**: Direct file streaming with MIME type detection
- **Protocol**: SOAP 1.1 via manual XML handling

## Development

### Building from Source

```bash
# Clone repository
git clone https://github.com/jakepi84/JellyfinSonos.git
cd JellyfinSonos

# Restore dependencies
dotnet restore

# Build
dotnet build --configuration Release

# Output will be in:
# Jellyfin.Plugin.JellyfinSonos/bin/Release/net9.0/Jellyfin.Plugin.JellyfinSonos.dll
```

### Testing Locally

1. Build the plugin
2. Copy DLL to Jellyfin plugins directory
3. Restart Jellyfin
4. Configure plugin in dashboard
5. Test endpoints:
   ```bash
   # Test strings endpoint
   curl https://your-server.com/sonos/strings.xml
   
   # Test presentation map
   curl https://your-server.com/sonos/presentationMap.xml
   
   # Test SMAPI endpoint (requires SOAP request)
   curl -X POST https://your-server.com/sonos/smapi \
     -H "Content-Type: text/xml" \
     -d @test-request.xml
   ```

### Project Structure

```
Jellyfin.Plugin.JellyfinSonos/
├── Api/
│   ├── SmapiController.cs      # SOAP endpoint handler
│   └── SonosController.cs      # REST API endpoints
├── Configuration/
│   ├── PluginConfiguration.cs  # Configuration model
│   └── configPage.html         # Dashboard UI
├── Services/
│   ├── ISonosService.cs        # SMAPI interface
│   ├── SonosService.cs         # SMAPI implementation
│   ├── JellyfinMusicService.cs # Jellyfin integration
│   ├── OAuthService.cs         # OAuth token + code helpers
│   └── Sonoswsdl-*.wsdl        # Sonos WSDL specification
└── Plugin.cs                    # Main plugin class
```

### Contributing

Contributions are welcome! Areas for improvement:

1. **Transcoding Support**: Add on-the-fly transcoding for incompatible formats
2. **Playlist Support**: Implement Jellyfin playlist browsing
3. **Enhanced Search**: Add more search filters and options
4. **Caching**: Add metadata caching for better performance
5. **Localization**: Add translations for more languages

Please open an issue or pull request on GitHub.

## Frequently Asked Questions

### Can I use this with Sonos S1?

Yes, but with limitations:
- S1 doesn't support 24-bit FLAC files
- Some newer audio formats may not work
- Consider transcoding incompatible files in Jellyfin

### Does this work with Sonos Arc/Beam/Ray soundbars?

Yes! The plugin works with all Sonos products that support music services, including soundbars.

### Can I use this locally without internet access?

Yes, if:
- Jellyfin server is on your local network
- Sonos devices are on same network
- You have an SSL certificate (even self-signed)
- You still need Sonos Developer Portal registration

### Why do I need HTTPS?

Sonos requires HTTPS for all music services for security. You can use:
- Let's Encrypt (free, automated)
- Cloudflare (free, with proxy)
- Self-signed certificate (requires manual trust setup)

### Will my music play without internet?

Once set up, music streams locally from your Jellyfin server to Sonos. No internet required for playback, only for initial setup.

### Can multiple family members use this?

Yes! Each user can authorize their own Jellyfin account. Sonos will show a dropdown to switch between users in the service settings.

### What happens if I change my Jellyfin password?

You'll need to re-authorize in the Sonos app:
- Settings → Services → Your Service → Reauthorize Account

## Known Limitations

1. **No Transcoding**: Plugin streams files as-is. Unsupported formats won't play.
2. **No Offline Playback**: Requires active connection to Jellyfin server.
3. **Limited Playlist Support**: Currently focused on albums and artists.
4. **No Favorites Sync**: Sonos favorites don't sync back to Jellyfin.
5. **Single Library**: Only works with the default music library per user.

## Roadmap

Future enhancements planned:

- [ ] On-the-fly transcoding support
- [ ] Jellyfin playlist integration
- [ ] Smart playlists / dynamic queries
- [ ] Enhanced metadata (lyrics, ratings)
- [ ] Better error messages in Sonos app
- [ ] Admin dashboard for monitoring
- [ ] Multi-library support

## Credits

This plugin is inspired by and references the [bonob](https://github.com/simojenki/bonob) project, which provides a similar integration for Subsonic-compatible music servers.

Special thanks to:
- Jellyfin team for the excellent plugin framework
- Sonos for the SMAPI specification
- bonob project for SMAPI implementation patterns

## Support

Having issues? Please:

1. Check the [Troubleshooting](#troubleshooting) section above
2. Search [existing GitHub issues](https://github.com/jakepi84/JellyfinSonos/issues)
3. Review Jellyfin server logs for errors
4. Open a new issue with:
   - Plugin version
   - Jellyfin version
   - Sonos product model
   - Error messages / logs
   - Steps to reproduce

## License

This project is licensed under the GPL-3.0 License - see the [LICENSE](LICENSE) file for details.

## Disclaimer

This plugin is provided as-is without warranty. Use at your own risk. Ensure you:
- Comply with Sonos developer terms of service
- Properly secure your Jellyfin server when exposing it to the internet
- Use strong passwords and encryption keys
- Keep software updated for security patches

Not affiliated with Jellyfin or Sonos. All trademarks belong to their respective owners.