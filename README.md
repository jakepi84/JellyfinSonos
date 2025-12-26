# Jellyfin Sonos Plugin

A Jellyfin plugin that integrates with Sonos speakers using the Sonos Music API (SMAPI). This plugin allows you to access your Jellyfin music library directly from the Sonos app without requiring local device discovery or auto-registration.

## Features

- **SMAPI Integration**: Implements the Sonos Music API Protocol for seamless integration with Sonos devices
- **No Auto-Discovery**: Designed for manual registration via the Sonos Developer Portal
- **Secure Authentication**: Uses encrypted tokens for secure credential management
- **Browse Music Library**: Access your Jellyfin music library organized by artists and albums
- **Search**: Search for artists, albums, and tracks from the Sonos app
- **Direct Streaming**: Stream music directly from your Jellyfin server to Sonos devices

## Requirements

- Jellyfin Server 10.10.0 or later
- .NET 9.0
- A Sonos Developer Account (for registering the service)
- Your Jellyfin server must be accessible via a public URL (or at least accessible from your Sonos devices)

## Installation

1. Download the latest release of the plugin
2. Extract the plugin files to your Jellyfin plugins directory:
   - Linux: `/var/lib/jellyfin/plugins/JellyfinSonos/`
   - Windows: `%APPDATA%\Jellyfin\Server\plugins\JellyfinSonos\`
3. Restart your Jellyfin server
4. Navigate to **Dashboard** → **Plugins** → **Jellyfin Sonos** to configure the plugin

## Configuration

1. **Service Name**: The name that will appear in your Sonos app (default: "Jellyfin")
2. **Service ID**: A unique service ID for Sonos (default: 247) - must be unique across all services you use
3. **External URL**: The public URL of your Jellyfin server (e.g., `https://jellyfin.example.com`)
   - This URL **must** be accessible from your Sonos devices
4. **Secret Key**: A secure random string used for encrypting credentials (generate a strong password)

### Generating a Secret Key

You can generate a secure secret key using:

```bash
# Linux/Mac
openssl rand -base64 32

# Windows PowerShell
$bytes = New-Object Byte[] 32
[Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
[Convert]::ToBase64String($bytes)
```

## Registering with Sonos Developer Portal

**Note**: As of the latest Sonos policies, auto-registration is no longer supported. You must register your service through the Sonos Developer Portal.

1. Go to the [Sonos Developer Portal](https://developer.sonos.com/)
2. Create an account or log in
3. Register a new SMAPI service with the following details:
   - **Service Name**: Same as configured in the plugin
   - **SMAPI Endpoint**: `https://your-jellyfin-server.com/sonos/smapi`
   - **Authentication**: AppLink
   - **Strings URL**: `https://your-jellyfin-server.com/sonos/strings.xml`
   - **Presentation Map URL**: `https://your-jellyfin-server.com/sonos/presentationMap.xml`
4. Submit for approval and wait for Sonos to review your service

## Using the Plugin

Once approved by Sonos:

1. Open the Sonos app on your mobile device or desktop
2. Go to **Settings** → **Services & Voice** → **Music & Content**
3. Tap **Add a Service**
4. Find your service name (e.g., "Jellyfin") in the list
5. Tap **Add to Sonos**
6. You'll be redirected to a login page - enter your Jellyfin credentials
7. Once authorized, you can browse and play your Jellyfin music library from the Sonos app

## Architecture

This plugin implements:

- **SMAPI SOAP Service**: Handles Sonos API requests for browsing and playback
- **Authentication Flow**: Implements AppLink authentication for secure user authorization
- **REST API Endpoints**: Provides login, streaming, and configuration endpoints
- **Jellyfin Integration**: Integrates with Jellyfin's library manager to access music content

### Key Endpoints

- `/sonos/login` - Login page for device authorization
- `/sonos/authorize` - POST endpoint for credential validation
- `/sonos/smapi` - SOAP endpoint for Sonos API calls
- `/sonos/stream/{trackId}` - Audio streaming endpoint
- `/sonos/strings.xml` - Localization strings
- `/sonos/presentationMap.xml` - UI configuration

## Development

### Building

```bash
dotnet build Jellyfin.Plugin.JellyfinSonos.sln
```

### Testing

The plugin can be tested locally by:

1. Building the plugin
2. Copying the DLL to your Jellyfin plugins directory
3. Restarting Jellyfin
4. Accessing the SOAP endpoint to verify it's working

## Credits

This plugin is inspired by and references the [bonob](https://github.com/simojenki/bonob) project, which provides a similar integration for Subsonic-compatible music servers.

## License

This project is licensed under the GPL-3.0 License - see the LICENSE file for details.

## Disclaimer

This plugin is provided as-is. Use at your own risk. Make sure you comply with Sonos developer terms of service and your Jellyfin server is properly secured when exposing it to the internet.