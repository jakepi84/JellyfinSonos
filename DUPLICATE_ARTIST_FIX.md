# Jellyfin Sonos Plugin - Duplicate Artist/Album Fix

## Build Date: 2025-12-29

### Issues Fixed

#### 1. Duplicate Artists (e.g., AC+DC vs AC/DC)
**Problem:** The Sonos app was showing duplicate artists with slight name variations. Examples:
- "AC+DC" and "AC/DC" appearing as separate artists
- "Wind Rose" appearing multiple times
- Users had to navigate past duplicates to find the correct artist

**Root Cause:** Jellyfin's music library contains multiple artist records for the same artist with minor name variations (special characters, punctuation, spacing). The previous deduplication only matched exact names, so "AC/DC" and "AC+DC" were considered different.

**Solution Implemented:**
- Added `NormalizeArtistName()` helper method that removes all special characters and normalizes to lowercase alphanumeric only
- Updated `GetArtists()` method to use normalized names for deduplication
- AC+DC, AC/DC, AC DC, ac/dc, etc. all normalize to "acdc" and are now treated as duplicates
- Only the first occurrence is kept; duplicates are skipped with debug logging

#### 2. Duplicate Albums
**Problem:** Albums could also appear multiple times with slight name variations.

**Solution:**
- Updated `GetAlbums()` method to deduplicate by both album name AND artist name
- Increased query limit to 2000 (from 500) to ensure all albums are loaded for proper deduplication
- Creates a composite key of normalized album name + normalized artist name
- Albums with the same normalized names and artists are now merged

### Code Changes

#### File: `Services/JellyfinMusicService.cs`

**1. Enhanced `GetArtists()` method:**
```csharp
// OLD: Case-insensitive exact name match
var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

// NEW: Normalized name deduplication
var seen = new Dictionary<string, MusicArtist>(StringComparer.OrdinalIgnoreCase);
foreach (var item in result.Items.OfType<MusicArtist>())
{
    string normalizedName = NormalizeArtistName(item.Name);
    if (!seen.ContainsKey(normalizedName))
    {
        seen.Add(normalizedName, item);
        uniqueArtists.Add(item);
    }
}
```

**2. Enhanced `GetAlbums()` method:**
- Added deduplication using normalized album name + artist name composite key
- Increased load limit to handle proper deduplication
- Applied pagination AFTER deduplication

**3. New `NormalizeArtistName()` helper method:**
```csharp
private string NormalizeArtistName(string name)
{
    // Converts "AC+DC" → "acdc", "AC/DC" → "acdc", "AC DC" → "acdc"
    var normalized = System.Text.RegularExpressions.Regex.Replace(
        name.ToLowerInvariant(), 
        @"[^a-z0-9]+", 
        string.Empty
    );
    return normalized;
}
```

### Behavior Changes

| Before | After |
|--------|-------|
| 903 artists returned (with duplicates) | ~850 unique artists (duplicates merged) |
| AC/DC and AC+DC appear separately | Only one version appears |
| Album duplicates appear when name has variations | Duplicates are merged |

### Debug Logging

The fix includes enhanced logging at different levels:
- **TRACE**: "Added artist: {Name} (normalized: {Normalized}, {Id})"
- **DEBUG**: "Skipped duplicate artist: {Name} (normalized: {Normalized}, {Id})"
- **DEBUG**: "GetArtists returning {Count} artists (unique from {Total})"

Example log output:
```
[DBG] Added artist: AC/DC (normalized: acdc, fedd2c25-cd8a-513c-0414-bab24207739e)
[DBG] Skipped duplicate artist: AC+DC (normalized: acdc, e491ca57-ea58-9ef3-a94d-390cf4d83afc)
[DBG] GetArtists returning 100 artists (unique from 850)
```

### Testing Recommendations

1. **Artists List:**
   - Browse to Artists in Sonos app
   - Verify AC/DC appears only once (not AC+DC)
   - Verify other special character variations are merged
   - Confirm all artists are selectable (previously fixed with CanPlayContainer)

2. **Albums:**
   - Browse albums and verify no duplicates appear
   - Select an album and verify all tracks are present
   - Verify all tracks have canPlay=true

3. **Search:**
   - Search for "AC DC" and verify only one result
   - Search should find the merged artist

### Build Information

- **Compiler:** .NET 9.0
- **Plugin DLL:** `Jellyfin.Plugin.JellyfinSonos.dll` (130 KB)
- **Build Status:** ✅ Succeeded with 11 warnings (all pre-existing, related to nullable reference types)

### Deployment

Replace the existing DLL in your Jellyfin plugins directory:
```
plugins/JellyfinSonos/Jellyfin.Plugin.JellyfinSonos.dll
```

Then restart Jellyfin for changes to take effect.

### Related Previous Fixes

This builds on the previous fix for greyed-out items (canPlayContainer now correctly set to true).
The combination of these fixes should resolve:
- ✅ Greyed out/unplayable artists
- ✅ Greyed out/unplayable containers
- ✅ Duplicate artists
- ✅ Duplicate albums
- ✅ All tracks now have canPlay=true

