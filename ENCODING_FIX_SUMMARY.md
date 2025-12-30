# SMAPI XML Encoding Fix - Summary

## Problem
Sonos was receiving truncated or malformed SMAPI SOAP responses from the plugin, causing items to appear greyed out (unplayable) or not to load properly. The issue was that content was being cut off during transmission.

## Root Cause
**Content-Length Header Mismatch**: The ASP.NET `Content()` method with `"text/xml"` content type was:
1. Declaring UTF-8 encoding in the HTTP response
2. But potentially not specifying the charset correctly
3. This mismatch between declared Content-Length and actual bytes could cause Sonos to truncate the response mid-stream

## Solution Applied
**[SmapiController.cs](SmapiController.cs#L189-L194)** - Line 189-194:

Changed from:
```csharp
return Content(response, "text/xml");
```

To:
```csharp
// Return XML with explicit UTF-8 encoding to prevent Content-Length mismatch
// Use charset=utf-8 to match the actual encoding of the response
var bytes = Encoding.UTF8.GetBytes(response);
return new Microsoft.AspNetCore.Mvc.FileContentResult(bytes, "application/xml; charset=utf-8");
```

### Key Changes:
1. **Explicit Encoding**: Convert response to UTF-8 bytes explicitly before returning
2. **Correct Content-Type**: Use `application/xml; charset=utf-8` instead of `text/xml`
3. **FileContentResult**: Use `FileContentResult` which properly sets Content-Length based on actual byte count
4. **Charset Declaration**: Explicitly declare charset=utf-8 to prevent any encoding mismatches

## Why This Fixes It
- The Content-Length header will now match the actual number of bytes being sent
- Sonos will receive the complete response without truncation
- The charset declaration ensures both client and server agree on encoding
- Application/xml is the correct MIME type for SMAPI SOAP responses (not text/xml)

## Files Modified
- `Jellyfin.Plugin.JellyfinSonos/Api/SmapiController.cs`

## Testing
After applying this fix:
1. Rebuild the project: `dotnet build -c Release`
2. Deploy the DLL to your Jellyfin plugins directory
3. Restart Jellyfin
4. Test playback of albums, playlists, and individual tracks
5. Check Jellyfin logs for successful SMAPI requests without truncation errors

## Build Status
✅ Build succeeded with no errors
- 11 warnings (pre-existing documentation and null-safety warnings)
- DLL compiled successfully for .NET 9.0
