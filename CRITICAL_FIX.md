# CRITICAL FIX: Artist canPlay Flag

## Problem Found
The log analysis revealed that **artists were being marked with `canPlay=false`**, which made them unplayable in the Sonos UI. This is why all tracks appeared greyed out - Sonos was treating artists as non-playable containers.

## Root Cause
In `SonosService.cs`, the `GetArtists()` method at line 405 was setting:
```csharp
CanPlay = false,
```

for artist items returned to Sonos.

## Solution Applied
Changed line 405 to:
```csharp
CanPlay = true,
```

Artists are now marked as playable, which allows Sonos to:
1. Display them as selectable items
2. Enable the play button in the UI
3. Request media URIs when user selects an artist
4. Play all tracks by that artist

## Log Evidence
Before fix (from 1:27pm deployment log):
```xml
<ns:title>Béla Bánfalvi</ns:title>
<ns:itemType>artist</ns:itemType>
<ns:canPlay>false</ns:canPlay>
<ns:canPlayContainer>true</ns:canPlayContainer>
```

After fix: Artists will have `<ns:canPlay>true</ns:canPlay>`

## Files Modified
- `Jellyfin.Plugin.JellyfinSonos\Services\SonosService.cs` (line 405)

## Build Status
✅ Build succeeded (0 errors, 11 warnings)
✅ DLL rebuilt: 133,632 bytes
✅ DLL copied to deployment folder

## Related Changes (from previous fixes)
1. **Artist Deduplication** (JellyfinMusicService.cs): Now keeps artist with most albums
2. **Streaming URL**: Uses `/sonos/stream/{trackId}` endpoint with proper OAuth token

## Testing Required
1. Deploy updated DLL to Jellyfin
2. Clear any cached metadata
3. Test browsing artists in Sonos - should now show selectable (not greyed out)
4. Test playing an artist - should now allow playback
