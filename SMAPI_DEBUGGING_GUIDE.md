# SMAPI Protocol Debugging & Integration Guide

## Overview

This guide explains how to debug and understand SMAPI (Sonos Music API) protocol interactions in the Jellyfin Sonos Plugin. It covers request/response parsing, authentication, and common debugging scenarios.

## Table of Contents

1. [SMAPI Protocol Basics](#smapi-protocol-basics)
2. [Understanding SOAP Requests](#understanding-soap-requests)
3. [Understanding SOAP Responses](#understanding-soap-responses)
4. [Authentication Flow](#authentication-flow)
5. [Common SMAPI Methods](#common-smapi-methods)
6. [Debugging Techniques](#debugging-techniques)
7. [Log Analysis](#log-analysis)
8. [Common Issues & Solutions](#common-issues--solutions)

---

## SMAPI Protocol Basics

### What is SMAPI?

SMAPI (Sonos Music API) is a SOAP-based protocol that allows third-party music services to integrate with Sonos devices. The protocol is defined in the WSDL specification and uses XML for all communication.

### Protocol Stack

- **Transport**: HTTP/HTTPS
- **Message Format**: XML (SOAP)
- **Endpoint**: `/sonos/smapi`
- **Method Invocation**: SOAP method calls with XML body

### Key Components

1. **SOAP Envelope**: XML wrapper for the request/response
2. **SOAP Header**: Contains credentials and context information
3. **SOAP Body**: Contains the actual method call and parameters
4. **Fault Responses**: XML error responses from the server

---

## Understanding SOAP Requests

### Request Structure

```xml
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
              xmlns:ns="http://www.sonos.com/Services/1.1">
  <soap:Header>
    <credentials xmlns="http://www.sonos.com/Services/1.1">
      <deviceProvider>Sonos</deviceProvider>
      <loginToken>
        <householdId>Sonos_EuNctAauMm4wNvVBg7JGgqpYeD_cf22c4fe</householdId>
      </loginToken>
    </credentials>
    <ns:context>
      <ns:timeZone>-06:00</ns:timeZone>
    </ns:context>
  </soap:Header>
  <soap:Body>
    <ns:getMetadata xmlns="http://www.sonos.com/Services/1.1">
      <id>root</id>
      <index>0</index>
      <count>100</count>
    </ns:getMetadata>
  </soap:Body>
</soap:Envelope>
```

### Request Headers Section

The `<soap:Header>` contains two key parts:

#### 1. Credentials

For **authenticated requests** (after user login):
```xml
<credentials xmlns="http://www.sonos.com/Services/1.1">
  <deviceProvider>Sonos</deviceProvider>
  <loginToken>
    <token>N2M1NDk2ZTItZTViMC00YTAzLTk0ZTktOTE5NWFlODhmMjkzfGpha2V8MT...</token>
    <key>N2M1NDk2ZTItZTViMC00YTAzLTk0ZTktOTE5NWFlODhmMjkzfGpha2V8MT...</key>
    <householdId>Sonos_EuNctAauMm4wNvVBg7JGgqpYeD_c28232eb</householdId>
  </loginToken>
</credentials>
```

For **unauthenticated requests** (initial setup):
```xml
<credentials xmlns="http://www.sonos.com/Services/1.1">
  <deviceId>00-00-00-00-00-00:0</deviceId>
  <deviceProvider>Sonos</deviceProvider>
</credentials>
```

#### 2. Context

```xml
<ns:context>
  <ns:timeZone>-06:00</ns:timeZone>
</ns:context>
```

The context includes timezone information that the service can use for display purposes.

### Request Body Examples

#### getMetadata (Browse Content)

```xml
<ns:getMetadata xmlns="http://www.sonos.com/Services/1.1">
  <id>root</id>
  <index>0</index>
  <count>100</count>
</ns:getMetadata>
```

**Parameters:**
- `id`: Content ID to retrieve (e.g., "root", "artists", "artist:UUID")
- `index`: Starting index for pagination (0-based)
- `count`: Number of items to return

#### getExtendedMetadata (Get Details)

```xml
<ns:getExtendedMetadata xmlns="http://www.sonos.com/Services/1.1">
  <id>artist:e491ca57-ea58-9ef3-a94d-390cf4d83afc</id>
</ns:getExtendedMetadata>
```

**Parameters:**
- `id`: Content ID to get extended information for

#### getAppLink (Initiate Authentication)

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Header>
    <credentials xmlns="http://www.sonos.com/Services/1.1">
      <deviceId>00-00-00-00-00-00:0</deviceId>
      <deviceProvider>Sonos</deviceProvider>
    </credentials>
  </s:Header>
  <s:Body>
    <getAppLink xmlns="http://www.sonos.com/Services/1.1">
      <householdId>Sonos_EuNctAauMm4wNvVBg7JGgqpYeD_c28232eb</householdId>
      <hardware>iPhone17,5</hardware>
      <osVersion>Version 26.2 (Build 23C55)</osVersion>
      <sonosAppName>ICRU_iPhone17,5</sonosAppName>
      <callbackPath>sonos-2://x-callback-url/addAccount?state=intId%3Dnet%2Ejakepi%2Ejellyfin</callbackPath>
    </getAppLink>
  </s:Body>
</s:Envelope>
```

**Parameters:**
- `householdId`: Sonos household identifier
- `hardware`: Device model (e.g., "iPhone17,5")
- `osVersion`: Operating system version
- `sonosAppName`: Sonos app identifier
- `callbackPath`: URL scheme for callback after authentication

#### getDeviceAuthToken (Get Auth Token)

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Header>
    <credentials xmlns="http://www.sonos.com/Services/1.1">
      <deviceId>00-00-00-00-00-00:0</deviceId>
      <deviceProvider>Sonos</deviceProvider>
    </credentials>
  </s:Header>
  <s:Body>
    <getDeviceAuthToken xmlns="http://www.sonos.com/Services/1.1">
      <householdId>Sonos_EuNctAauMm4wNvVBg7JGgqpYeD_c28232eb</householdId>
      <linkCode>G925WRSD5V8T1YRC</linkCode>
    </getDeviceAuthToken>
  </s:Body>
</s:Envelope>
```

**Parameters:**
- `householdId`: Sonos household identifier
- `linkCode`: Link code obtained from `getAppLink` response

#### reportAccountAction (Account Management)

```xml
<s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/">
  <s:Header>
    <credentials xmlns="http://www.sonos.com/Services/1.1">
      <deviceProvider>Sonos</deviceProvider>
      <loginToken>
        <token>N2M1NDk2ZTItZTViMC00YTAzLTk0ZTktOTE5NWFlODhmMjkzfGpha2V8MT...</token>
        <key>N2M1NDk2ZTItZTViMC00YTAzLTk0ZTktOTE5NWFlODhmMjkzfGpha2V8MT...</key>
        <householdId>Sonos_EuNctAauMm4wNvVBg7JGgqpYeD_c28232eb</householdId>
      </loginToken>
    </credentials>
  </s:Header>
  <s:Body>
    <reportAccountAction xmlns="http://www.sonos.com/Services/1.1">
      <type>addAccount</type>
    </reportAccountAction>
  </s:Body>
</s:Envelope>
```

**Parameters:**
- `type`: Account action (e.g., "addAccount", "removeAccount")

---

## Understanding SOAP Responses

### Successful Response Structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
              xmlns:ns="http://www.sonos.com/Services/1.1">
  <soap:Body>
    <ns:getMetadataResponse>
      <getMetadataResult>
        <count>5</count>
        <index>0</index>
        <total>5</total>
        <mediaCollection>
          <id>artists</id>
          <title>Artists</title>
          <itemType>container</itemType>
        </mediaCollection>
        <!-- Additional items... -->
      </getMetadataResult>
    </ns:getMetadataResponse>
  </soap:Body>
</soap:Envelope>
```

### Response Body Elements

#### Container Response (getMetadata)

```xml
<getMetadataResult>
  <count>5</count>
  <index>0</index>
  <total>5</total>
  
  <!-- Container items -->
  <mediaCollection>
    <id>artists</id>
    <title>Artists</title>
    <itemType>container</itemType>
  </mediaCollection>
  
  <!-- Track items -->
  <mediaMetadata>
    <id>track:uuid</id>
    <title>Song Title</title>
    <mimeType>audio/mpeg</mimeType>
    <itemType>track</itemType>
    <displayType>song</displayType>
  </mediaMetadata>
  
  <!-- Album items -->
  <mediaMetadata>
    <id>album:uuid</id>
    <title>Album Title</title>
    <mimeType>audio/mpeg</mimeType>
    <itemType>album</itemType>
  </mediaMetadata>
</getMetadataResult>
```

### Fault Response Structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/" 
              xmlns:ns="http://www.sonos.com/Services/1.1">
  <soap:Body>
    <soap:Fault>
      <faultcode>soap:Server</faultcode>
      <faultstring>Invalid Link Code</faultstring>
      <detail>
        <ns:authentication-fault>
          <reason>Link code not yet associated with a user</reason>
        </ns:authentication-fault>
      </detail>
    </soap:Fault>
  </soap:Body>
</soap:Envelope>
```

### Common Fault Codes

| Fault Code | Meaning | Action |
|-----------|---------|--------|
| `InvalidAuthToken` | Authentication token expired or invalid | Re-authenticate user |
| `InvalidLinkCode` | Link code not associated with user | User needs to log in |
| `NotImplemented` | Method not implemented | Check plugin version |
| `InvalidInput` | Invalid parameters | Validate request parameters |

---

## Authentication Flow

### Authentication Sequence

```
┌─────────────┐
│   Sonos     │
│    App      │
└──────┬──────┘
       │ 1. getAppLink()
       ├─────────────────────────────────────────┐
       │                                         │
       │                          ┌──────────────▼─────────┐
       │                          │  JellyfinSonos Plugin  │
       │  2. Returns:             │                        │
       │     - linkCode           │ - Generates linkCode   │
       │     - regUrl             │ - Stores in database   │
       │                          │ - Returns to Sonos     │
       │                          └──────────────┬─────────┘
       │                                        │
       │                                        │
       │ 3. User visits regUrl and logs in     │
       │    (via browser/plugin UI)            │
       │                                        │
       │ 4. getDeviceAuthToken()               │
       │    (with linkCode)                    │
       ├─────────────────────────────────────────┤
       │                                         │
       │                          ┌──────────────▼─────────┐
       │                          │  JellyfinSonos Plugin  │
       │  5. Returns:             │                        │
       │     - authToken          │ - Looks up linkCode    │
       │       (encrypted)        │ - Generates auth token │
       │       - userID           │ - Returns to Sonos     │
       │       - privURL          │                        │
       │       - serviceToken     │                        │
       │                          └────────────────────────┘
       │
       │ 6. Future requests use authToken
       ├─────────────────────────────────────────┐
       │ (getMetadata, getExtendedMetadata, etc) │
       │                                         │
       │                          ┌──────────────▼─────────┐
       │                          │  JellyfinSonos Plugin  │
       │                          │                        │
       │  7. Validates:           │ - Decrypts authToken   │
       │     - authToken          │ - Extracts userID      │
       │     - getUserData()      │ - Performs action      │
       │                          │ - Returns XML response │
       │                          └────────────────────────┘
       │
       └─────────────────────────────────────────┘
```

### Step 1: getAppLink

**Request:**
```
No existing authentication required
Device info provided (model, OS version, etc)
Callback URL for post-login redirect
```

**Response:**
- `linkCode`: 16-character alphanumeric code
- `regUrl`: URL where user logs in and associates device
- `deviceId`: Service-assigned device identifier (if applicable)

### Step 2: User Authorization

User visits the registration URL and logs into Jellyfin. The plugin stores the association between:
- `linkCode` → `userId` (from Jellyfin)
- `householdId` → Device info

### Step 3: getDeviceAuthToken

**Request:**
```
linkCode from Step 1
householdId of the device
```

**Response:**
```xml
<authToken>
  N2M1NDk2ZTItZTViMC00YTAzLTk0ZTktOTE5NWFlODhmMjkzfGpha2V8MT...
</authToken>
<userID>jake</userID>
<privURL>
  https://jellyfin.jakepi.net/sonos/
</privURL>
```

The authToken contains:
- Base64-encoded JSON payload with:
  - User ID
  - Expiration timestamp
  - Service identifier
- HMAC signature for verification

### Step 4: Subsequent Requests

All authenticated requests include the authToken in the SOAP header:

```xml
<loginToken>
  <token>{authToken}</token>
  <key>{authToken}</key>
  <householdId>{householdId}</householdId>
</loginToken>
```

The plugin:
1. Extracts the token from the header
2. Decodes the base64 payload
3. Verifies the signature
4. Extracts userId and checks expiration
5. Uses userId to retrieve user data from Jellyfin

---

## Common SMAPI Methods

### Content Browsing

#### `getMetadata(id, index, count)`
Returns metadata for a container (browse operation).

**Use Cases:**
- Browse artists list
- Browse albums by artist
- Browse tracks by album
- Browse playlists

**Response Includes:**
- Item count, index, and total
- Array of `mediaCollection` (containers) and `mediaMetadata` (tracks/albums)
- Can include extended metadata URLs for Sonos apps

#### `getExtendedMetadata(id)`
Returns detailed metadata for an item.

**Use Cases:**
- Get artist album art and bio
- Get album cover art
- Get track details (duration, artist, album)

**Response Includes:**
- Album art URI
- Related content links
- Display attributes

### Authentication Methods

#### `getAppLink(householdId, hardware, osVersion, sonosAppName, callbackPath)`
Initiates the authentication flow.

**Sonos Provides:**
- Device hardware identifier
- OS version
- Sonos app version
- Callback URL for post-login

**Plugin Responds With:**
- Unique link code
- Registration URL
- Device ID (optional)

#### `getDeviceAuthToken(householdId, linkCode)`
Exchanges the link code for an authentication token.

**Preconditions:**
- User must have visited registration URL
- User must have logged in

**Plugin Responds With:**
- Authentication token (encrypted)
- User ID
- Service URLs
- Private URL for service

### Account Management

#### `reportAccountAction(type)`
Reports account actions to the service.

**Action Types:**
- `addAccount`: User added account to Sonos
- `removeAccount`: User removed account from Sonos
- `accountCreated`: Account created event

**Plugin Uses This For:**
- Logging
- Analytics
- Cleanup operations

---

## Debugging Techniques

### 1. Enable Detailed Logging

In the plugin configuration or Jellyfin settings, enable DEBUG or TRACE level logging for the Sonos plugin.

### 2. Log Analysis

#### Finding SMAPI Requests

```bash
grep -n "SMAPI SOAP Request" jellyfin20251229_003.log | head -20
```

This shows timestamps and line numbers of all SMAPI requests.

#### Extracting Complete Request/Response Pair

```bash
# Find a specific request and show with context
grep -A 50 'getMetadata.*artists' jellyfin20251229_003.log
```

#### Filtering by Authentication Status

```bash
# Show all unauthenticated requests
grep "No auth token found" jellyfin20251229_003.log

# Show all authenticated requests
grep "Auth token found" jellyfin20251229_003.log
```

### 3. Token Inspection

The logs include decoded token payloads (without signature):

```
[DBG] Successfully parsed token:
userId=7c5496e2-e5b0-4a03-94e9-9195ae88f293,
expires=01/28/2026 14:18:01 +00:00
```

This shows:
- **userId**: Jellyfin user UUID
- **expires**: Token expiration date/time in UTC

### 4. Response Status Tracking

Monitor the flow:

```bash
# Show all method calls
grep "SMAPI method called" jellyfin20251229_003.log

# Show method responses
grep "SMAPI SOAP Response" jellyfin20251229_003.log
```

### 5. Network Tracing with Fiddler/Wireshark

For packet-level debugging:

**On Windows:**
```powershell
# Start Fiddler in command line
"C:\Program Files\Telerik\Fiddler\Fiddler.exe"

# Or use Wireshark
"C:\Program Files\Wireshark\Wireshark.exe"
```

Configure Sonos device to proxy through local machine to capture HTTPS traffic.

### 6. Unit Testing SOAP Parsing

Create test cases for SOAP parsing:

```csharp
[Fact]
public void TestGetMetadataRequest()
{
    var soapRequest = @"<soap:Envelope...></soap:Envelope>";
    var doc = new XmlDocument();
    doc.LoadXml(soapRequest);
    
    // Assert parsing works
    Assert.NotNull(doc);
}
```

---

## Log Analysis

### Example Log Sequence: Complete Authentication

```
[2025-12-29 08:59:27.315 -06:00] [DBG] SMAPI SOAP Request: getAppLink
[2025-12-29 08:59:27.317 -06:00] [DBG] No auth token found in request
[2025-12-29 08:59:27.317 -06:00] [INF] SMAPI method called: "getAppLink"
[2025-12-29 08:59:27.319 -06:00] [INF] GetAppLink called: 
    householdId="Sonos_EuNctAauMm4wNvVBg7JGgqpYeD_c28232eb"
    linkCode="G925WRSD5V8T1YRC"
[2025-12-29 08:59:27.320 -06:00] [DBG] SMAPI SOAP Response: getAppLink response
```

This shows:
1. Sonos device initiates authentication
2. Plugin generates link code `G925WRSD5V8T1YRC`
3. Plugin returns registration URL to Sonos app

```
[2025-12-29 08:59:32.469 -06:00] [DBG] SMAPI SOAP Request: getDeviceAuthToken
[2025-12-29 08:59:32.469 -06:00] [DBG] No auth token found in request
[2025-12-29 08:59:32.470 -06:00] [INF] GetDeviceAuthToken called: linkCode="G925WRSD5V8T1YRC"
[2025-12-29 08:59:32.471 -06:00] [INF] Link code not yet associated, user needs to log in
[2025-12-29 08:59:32.472 -06:00] [DBG] SMAPI SOAP Response: Fault - Authentication required
```

Device checks for token → User hasn't logged in yet → Fault response

```
[2025-12-29 08:59:47.900 -06:00] [DBG] SMAPI SOAP Request: getDeviceAuthToken
[2025-12-29 08:59:47.900 -06:00] [DBG] No auth token found in request
[2025-12-29 08:59:47.900 -06:00] [INF] GetDeviceAuthToken called: linkCode="G925WRSD5V8T1YRC"
[2025-12-29 08:59:47.900 -06:00] [INF] Link code associated with user: linkCode="G925WRSD5V8T1YRC", username="jake"
[2025-12-29 08:59:47.901 -06:00] [INF] GetDeviceAuthToken returning authToken for user: "jake"
[2025-12-29 08:59:47.901 -06:00] [DBG] SMAPI SOAP Response: getDeviceAuthToken response
```

User logs in → Plugin finds linkCode association → Returns auth token

### Example Log Sequence: Authenticated Request

```
[2025-12-29 08:58:32.457 -06:00] [DBG] SMAPI SOAP Request: getMetadata for "artists"
[2025-12-29 08:58:32.457 -06:00] [DBG] Auth token found (length: 122)
[2025-12-29 08:58:32.457 -06:00] [INF] SMAPI method called: "getMetadata"
[2025-12-29 08:58:32.457 -06:00] [INF] getMetadata inputs: id="artists", index=0, count=100, recursive=False, authTokenLen=122
[2025-12-29 08:58:32.458 -06:00] [DBG] Successfully parsed token:
    userId=7c5496e2-e5b0-4a03-94e9-9195ae88f293
    expires=01/28/2026 14:18:01 +00:00
[2025-12-29 08:58:32.458 -06:00] [DBG] Extracted userId from token: 7c5496e2-e5b0-4a03-94e9-9195ae88f293
[2025-12-29 08:58:32.539 -06:00] [DBG] Retrieved configuration:
    ExternalUrl="https://jellyfin.jakepi.net"
    ServiceName="Jellyfin"
    ServiceId=247
[2025-12-29 08:58:32.540 -06:00] [DBG] SMAPI SOAP Response: getMetadata response with X items
```

This shows:
1. Request includes valid auth token
2. Plugin extracts and validates token
3. Plugin confirms token is not expired
4. Plugin fetches user's artists
5. Returns XML response

---

## Common Issues & Solutions

### Issue 1: "No auth token found in request"

**Symptom:**
```
[DBG] No auth token found in request
```

**Cause:**
This is normal for initial `getAppLink` and `getDeviceAuthToken` requests. These methods don't require authentication.

**Solution:**
Only authenticated browse methods (getMetadata, getExtendedMetadata) should include auth tokens.

**Fix:**
Verify request logs show auth tokens for getMetadata, etc:
```
[DBG] Auth token found (length: 122)
```

### Issue 2: "Link code not yet associated"

**Symptom:**
```
[INF] Link code not yet associated, user needs to log in: linkCode="..."
[DBG] SMAPI SOAP Response: Fault - ...
```

**Cause:**
User generated link code but hasn't visited the registration URL to log in.

**Solution:**
1. Verify the registration URL is correct (from `regUrl` in getAppLink response)
2. Ensure user can access the URL from their network
3. Have user complete the login process
4. Wait for device to poll getDeviceAuthToken again

**Fix:**
Check plugin logs for the registration URL:
```
[INF] GetAppLink returning: regUrl="https://jellyfin.jakepi.net/sonos/login?linkCode=..."
```

### Issue 3: Token Expired

**Symptom:**
```
[DBG] Auth token found (length: 122)
[DBG] Token has expired: expires=01/15/2026 14:18:01 +00:00
```

**Cause:**
User's authentication token has expired (default 90 days).

**Solution:**
User needs to remove and re-add account in Sonos app, which triggers new authentication flow.

**Fix:**
Implement token refresh mechanism or set longer expiration in plugin configuration.

### Issue 4: "Invalid Link Code"

**Symptom:**
```
[INF] Link code not found in database: linkCode="XXXX"
```

**Cause:**
Device is using a link code that doesn't exist or has expired.

**Solution:**
Have user restart the authentication flow by:
1. Remove Jellyfin from Sonos app
2. Re-add service
3. Complete login with new link code

### Issue 5: User Not Found After Auth

**Symptom:**
```
[DBG] Successfully parsed token: userId=7c5496e2-e5b0-4a03-94e9-9195ae88f293
[ERR] User not found in Jellyfin: userId=7c5496e2-e5b0-4a03-94e9-9195ae88f293
```

**Cause:**
User was authenticated, but Jellyfin user UUID doesn't match plugin's user database.

**Solution:**
Verify user still exists in Jellyfin:
1. Check Jellyfin Dashboard → Users
2. Look up user by ID
3. Recreate user if necessary
4. Trigger re-authentication

**Fix:**
Ensure user UUID in Jellyfin database matches token payload.

### Issue 6: SOAP Parsing Errors

**Symptom:**
```
[ERR] Error parsing SOAP request: ...
System.Xml.XmlException: Expected element...
```

**Cause:**
Malformed XML in request or missing namespace declarations.

**Solution:**
1. Check SOAP request in logs for XML validity
2. Verify namespace URIs are correct
3. Test with sample XML parser

**Fix:**
Add error handling for malformed SOAP in controller:
```csharp
try 
{
    doc.LoadXml(soapRequest);
}
catch (XmlException ex)
{
    _logger.LogError(ex, "Failed to parse SOAP request");
    return BadRequest("Invalid XML");
}
```

### Issue 7: "GetMetadata called but response empty"

**Symptom:**
```
[DBG] GetMetadata called for id: "root", index: 0, count: 100
[DBG] SMAPI SOAP Response: getMetadata response with 0 items
```

**Cause:**
User has no artists/albums, or Jellyfin service is not returning data.

**Solution:**
1. Verify Jellyfin library has music content
2. Verify user has permission to access library
3. Check Jellyfin logs for API errors
4. Verify Jellyfin music library scanner has completed

**Fix:**
Ensure user can browse library via Jellyfin web UI before testing Sonos.

---

## Advanced Debugging

### Capturing Full SOAP Messages

Enable TRACE level logging to capture full SOAP payloads:

```json
{
  "Logging": {
    "LogLevel": {
      "Jellyfin.Plugin.JellyfinSonos": "Trace"
    }
  }
}
```

### Performance Profiling

Monitor method execution time in logs:

```
[2025-12-29 08:58:32.457 -06:00] GetMetadata start
[2025-12-29 08:58:32.540 -06:00] GetMetadata end (83ms)
```

Look for methods taking >1000ms and optimize:
- API calls (add caching)
- XML generation (optimize serialization)
- Database queries (add indexes)

### Load Testing

Generate multiple concurrent SMAPI requests:

```bash
# Using Apache Bench
ab -n 100 -c 10 \
  -H "Content-Type: application/xml" \
  -p smapi_request.xml \
  https://jellyfin.jakepi.net/sonos/smapi
```

Monitor:
- Response times
- Error rates
- Memory usage
- Thread pool utilization

---

## Testing Checklist

Before deploying to production, verify:

- [ ] `getAppLink` returns valid link code and registration URL
- [ ] Registration URL accessible and user can log in
- [ ] `getDeviceAuthToken` returns auth token after user logs in
- [ ] Auth token can be parsed and validated
- [ ] `getMetadata` returns correct artist/album structure
- [ ] `getExtendedMetadata` returns album art and metadata
- [ ] Search functionality works with various query strings
- [ ] Multiple concurrent requests handled correctly
- [ ] Token expiration handled gracefully
- [ ] Error responses properly formatted as SOAP Faults
- [ ] Sonos device can browse library
- [ ] Sonos device can play tracks
- [ ] Removing account cleans up link codes
- [ ] Re-adding account works after removal

---

## References

- [Sonos SMAPI Documentation](https://developer.sonos.com/reference/smapi-documentation/)
- [SOAP Protocol Specification](https://www.w3.org/TR/soap12/)
- [Jellyfin API Documentation](https://api.jellyfin.org/)
- [JellyfinSonos Plugin Repository](https://github.com/jakepi84/JellyfinSonos)

---

## Summary

The SMAPI protocol is a SOAP-based XML API for Sonos music service integration. Key concepts:

1. **Request/Response**: XML-based SOAP messages with headers and body
2. **Authentication**: Multi-step process with link codes and encrypted tokens
3. **Content Browsing**: Hierarchical navigation through getMetadata
4. **Metadata**: Extended information retrieved via getExtendedMetadata
5. **Error Handling**: SOAP Faults for error responses

By understanding the protocol structure and analyzing logs, you can effectively debug SMAPI integration issues and optimize plugin performance.
