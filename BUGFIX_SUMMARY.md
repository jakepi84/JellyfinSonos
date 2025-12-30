# Jellyfin Sonos Plugin - Greyed Out Music / Unplayable Items Fix

## Release: 20251229_0941

### Critical Bug Found and Fixed

**Problem:** Artists, albums, and playlists were appearing greyed out (unselectable/unplayable) in the Sonos app, preventing users from browsing and playing music.

**Root Cause:** The `canPlayContainer` SMAPI attribute was being set to the same value as `canPlay` for all items. This caused problems:

1. **Artist Items**: When an artist's extended metadata was requested, the response had:
   ```xml
   <ns:canPlay>false</ns:canPlay>
   <ns:canPlayContainer>false</ns:canPlayContainer>
   ```
   This made the artist completely unselectable in the Sonos UI.

2. **Album and Playlist Items in Browse**: All browsable container items had `canPlayContainer` set to the same value as `canPlay`, which was incorrect. According to SMAPI specification:
   - `canPlay`: Whether the item itself can be played as a track
   - `canPlayContainer`: Whether the item can be browsed/opened to show its contents

### Changes Made

#### 1. Added Separate `CanPlayContainer` Property
**File:** `Services/ISonosService.cs`
- Added a new property `CanPlayContainer` (default: `true`) to the `MediaCollection` class
- This allows containers to be marked as browsable independently of whether they're directly playable

#### 2. Updated All Serialization Methods  
**File:** `Api/SmapiController.cs`
- `SerializeGetMetadata()`: Now uses `collection.CanPlayContainer` instead of `collection.CanPlay`
- `SerializeExtendedMetadata()`: Now uses `mediaCollection.CanPlayContainer` instead of `mediaCollection.CanPlay`

#### 3. Updated MediaCollection Creation Logic
**File:** `Services/SonosService.cs`
- **GetArtists()**: Set `CanPlayContainer = true` for artists (they should be browsable)
- **GetAllAlbums()**: Set `CanPlayContainer = true` for albums
- **GetAllPlaylists()**: Set `CanPlayContainer = true` for playlists
- **GetAlbumsByArtist()**: Set `CanPlayContainer = true` for albums in artist view
- **GetCollectionMetadata()**: Set `CanPlayContainer = true` for extended metadata responses
- **Search Results**: Set `CanPlayContainer = true` for artist and album search results
- **Root Menu Items**: Set `CanPlayContainer = true` for all navigation containers

#### 4. Fixed Artist Extended Metadata
**File:** `Services/SonosService.cs` (GetCollectionMetadata method)
- Changed artist's `CanPlay` from `false` to `true` in extended metadata responses
- This ensures artists have `<ns:canPlayContainer>true</ns:canPlayContainer>` in the SOAP response

### SMAPI Protocol Details

The SMAPI (Sonos Music API) uses two flags for container items:

| Flag | Meaning | Example Value |
|------|---------|---------------|
| `canPlay` | Item can be played directly as audio | `true` for albums, `false` for artists |
| `canPlayContainer` | Item is a browsable container | `true` for all browsable items |

**Correct Behavior:**
- Artist: `canPlay=false`, `canPlayContainer=true` (navigate into artist to see albums)
- Album: `canPlay=true`, `canPlayContainer=true` (can select album to play all tracks, or navigate into album to see tracks)
- Playlist: `canPlay=true`, `canPlayContainer=true` (can select playlist to play all tracks, or navigate into playlist to see tracks)
- Track: No `canPlayContainer` element (it's a leaf item, not a container)

### Testing Recommendations

1. Clear Sonos app cache and force refresh
2. Browse to Artists - should see list without greyed items
3. Select an artist - should see albums, all selectable
4. Select an album - should see tracks, all playable
5. Select a track - should play without errors
6. Verify playlists work the same way
7. Test search functionality

### Build Information

- **Compiler:** .NET 9.0
- **Plugin DLL:** `Jellyfin.Plugin.JellyfinSonos.dll` (132 KB)
- **Build Date:** 2025-12-29
- **Build Time:** 09:41

### Deployment

Extract the zip file to your Jellyfin plugins directory:
```
plugins/
  JellyfinSonos/
    Jellyfin.Plugin.JellyfinSonos.dll
```

Then restart Jellyfin for the changes to take effect.
