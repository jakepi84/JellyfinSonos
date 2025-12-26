# Implementation Summary

## Overview
This implementation provides a complete Jellyfin plugin structure for integrating with Sonos speakers using the Sonos Music API (SMAPI). The plugin follows the requirement of NOT implementing auto-discovery or local registration, instead providing an endpoint that can be registered with the Sonos Developer Portal.

## Architecture

### Core Components

1. **Plugin.cs** - Main plugin entry point
   - Registers with Jellyfin plugin system
   - Provides configuration page
   - GUID: `a8c8f6d4-3b2e-4f1a-9c7d-8e5f6a7b8c9d`

2. **PluginConfiguration.cs** - Configuration model
   - ServiceName: Display name in Sonos app
   - ServiceId: Unique ID for Sonos (default: 247)
   - ExternalUrl: Public URL for Jellyfin server
   - SecretKey: Encryption key for credentials

3. **Services Layer**
   - **ISonosService.cs**: SMAPI service interface definition
   - **SonosService.cs**: Core SMAPI implementation
   - **JellyfinMusicService.cs**: Integration with Jellyfin library
   - **OAuthService.cs**: OAuth authorization code + refresh token helpers (signs access tokens)

4. **API Controllers**
   - **SonosController.cs**: REST endpoints (OAuth authorize/token, strings, presentationMap, stream)
   - **SmapiController.cs**: SOAP endpoint for SMAPI requests

5. **Configuration Page**
   - HTML-based configuration in Jellyfin dashboard
   - Form for setting service name, ID, URL, and secret key
   - Setup instructions

## Implementation Approach

### SMAPI Integration
The plugin implements SMAPI (Sonos Music API Protocol) using a manual SOAP handler rather than using SoapCore middleware. This approach was chosen because:
1. Jellyfin plugins don't have direct access to ASP.NET Core middleware pipeline
2. Manual SOAP handling provides more control and flexibility
3. Simpler integration with Jellyfin's controller-based API

### Authentication Flow (OAuth)
Implements OAuth 2.0 Authorization Code with refresh tokens (PKCE supported):
1. Sonos opens `/sonos/oauth/authorize` with `response_type=code`, `client_id`, `redirect_uri`, optional PKCE params
2. User signs in; plugin issues short-lived authorization code bound to client + redirect URI
3. Sonos exchanges the code at `/sonos/oauth/token` (`grant_type=authorization_code`) to receive access/refresh tokens (signed with plugin secret)
4. Sonos includes the access token as a bearer `Authorization` header for SMAPI calls
5. Refresh flow: Sonos calls `/sonos/oauth/token` with `grant_type=refresh_token` to rotate tokens

### No Auto-Discovery
As per requirements, the plugin does NOT implement:
- Local network discovery of Sonos devices
- Automatic registration with Sonos systems
- SSDP/UPnP device discovery

Instead, the service must be registered via Sonos Developer Portal using OAuth authentication.

## Endpoints

### SMAPI Endpoint
**POST /sonos/smapi**
- Content-Type: text/xml, application/xml
- Handles SOAP requests for:
  - getAppLink
  - getDeviceAuthToken
  - getMetadata
  - getMediaMetadata
  - getMediaURI
  - search
  - reportAccountAction

### REST Endpoints
- **GET /sonos/oauth/authorize** - OAuth authorization page (Authorization Code + PKCE)
- **POST /sonos/oauth/token** - OAuth token endpoint (authorization_code and refresh_token grants)
- **GET /sonos/strings.xml** - Localization strings
- **GET /sonos/presentationMap.xml** - UI configuration
- **GET /sonos/stream/{trackId}** - Audio streaming (requires bearer access token)

## Security

### Token Security
- Access tokens are signed with an HMAC derived from the configured secret key (no plaintext credentials in tokens)
- Authorization codes are single-use and expire after 10 minutes
- Refresh tokens expire after 30 days and are rotated on use
### OAuth Validation
- PKCE supported (`plain` and `S256`)
- Redirect URI and client ID are bound to authorization codes

### Validation
- User existence verified on authorization
- Link code validated before credential storage
- Authentication will be validated on streaming access

## Dependencies

- **Jellyfin.Model 10.10.3**: Jellyfin data models
- **Jellyfin.Controller 10.10.3**: Jellyfin controller interfaces
- **SoapCore 1.1.0.49**: SOAP service support (not actively used, can be removed)
- **System.ServiceModel.Primitives 8.0.0**: Service model types

## Build Output

The plugin builds to a single DLL:
- `Jellyfin.Plugin.JellyfinSonos.dll` - Main plugin assembly
- Target framework: .NET 9.0
- Compatible with Jellyfin 10.10.x

## Installation

1. Build the plugin: `dotnet build`
2. Copy `Jellyfin.Plugin.JellyfinSonos.dll` to Jellyfin plugins directory
3. Restart Jellyfin
4. Configure plugin in Dashboard → Plugins → Jellyfin Sonos
5. Register with Sonos Developer Portal
6. Wait for approval
7. Add service in Sonos app

## Areas Requiring Completion

The following areas are marked with TODO comments for production implementation:

### 1. Metadata Browsing
Location: `Services/SonosService.cs` - `GetMetadata()`
Needs:
- Extract user ID from SOAP credentials
- Make async calls to JellyfinMusicService
- Map Jellyfin artists/albums/tracks to SMAPI format
- Handle pagination

### 2. Audio Streaming
Location: `Api/SonosController.cs` - `Stream()`
Needs:
- Extract auth token from Authorization header
- Decrypt token to get credentials
- Authenticate user with Jellyfin
- Get audio file from library
- Return FileStreamResult with proper MIME type
- Handle transcoding if needed

### 3. Search Implementation
Location: `Services/SonosService.cs` - `Search()`
Needs:
- Call JellyfinMusicService.Search()
- Map results to SMAPI format
- Handle different search types (artists, albums, tracks)

### 4. User Context Handling
Multiple locations
Needs:
- Extract credentials from SOAP headers
- Maintain user context across SMAPI calls
- Validate credentials on each request

## Testing Recommendations

1. **Unit Tests**: Test service logic, encryption, link code management
2. **Integration Tests**: Test with actual Jellyfin server
3. **SOAP Tests**: Verify SOAP request/response parsing
4. **Security Tests**: Test authentication, encryption, link code expiration

## References

- **bonob project**: https://github.com/simojenki/bonob (SMAPI implementation reference)
- **Jellyfin plugin docs**: https://docs.jellyfin.org/general/server/plugins/
- **SMAPI spec**: Sonoswsdl-1.19.6-20231024.wsdl (included in plugin)
- **Sonos Developer Portal**: https://developer.sonos.com/

## License

GPL-3.0 (required by Jellyfin plugin licensing)
