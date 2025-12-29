using System;
using System.Collections.Concurrent;

namespace Jellyfin.Plugin.JellyfinSonos.Services;

/// <summary>
/// Association between link code and user authentication.
/// </summary>
public class LinkCodeAssociation
{
    /// <summary>
    /// Gets or sets the auth token (bearer token for SMAPI calls).
    /// </summary>
    public string AuthToken { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the user ID.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets when this association was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Manages Sonos AppLink link codes.
/// Generates unique link codes, associates them with user auth tokens,
/// and provides retrieval for getDeviceAuthToken flow.
/// </summary>
public class LinkCodeService
{
    private readonly ConcurrentDictionary<string, LinkCodeAssociation?> _linkCodes = new();
    private readonly TimeSpan _expirationTime = TimeSpan.FromHours(1);

    /// <summary>
    /// Generates a new unique link code.
    /// </summary>
    /// <returns>A unique link code string.</returns>
    public string GenerateLinkCode()
    {
        var linkCode = GenerateRandomString(16);
        _linkCodes.TryAdd(linkCode, null);
        CleanupExpiredCodes();
        return linkCode;
    }

    /// <summary>
    /// Checks if a link code exists and is valid.
    /// </summary>
    /// <param name="linkCode">The link code.</param>
    /// <returns>True if valid.</returns>
    public bool IsValid(string linkCode)
    {
        if (string.IsNullOrWhiteSpace(linkCode))
        {
            return false;
        }

        if (_linkCodes.TryGetValue(linkCode, out var association))
        {
            // Check if expired
            if (association != null && DateTime.UtcNow - association.CreatedAt > _expirationTime)
            {
                _linkCodes.TryRemove(linkCode, out _);
                return false;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Associates a link code with user authentication information.
    /// </summary>
    /// <param name="linkCode">The link code.</param>
    /// <param name="association">The user association.</param>
    /// <returns>True if successful.</returns>
    public bool Associate(string linkCode, LinkCodeAssociation association)
    {
        if (!IsValid(linkCode))
        {
            return false;
        }

        association.CreatedAt = DateTime.UtcNow;
        _linkCodes[linkCode] = association;
        return true;
    }

    /// <summary>
    /// Gets the association for a link code.
    /// </summary>
    /// <param name="linkCode">The link code.</param>
    /// <returns>The association or null.</returns>
    public LinkCodeAssociation? GetAssociation(string linkCode)
    {
        if (_linkCodes.TryGetValue(linkCode, out var association))
        {
            // Check if expired
            if (association != null && DateTime.UtcNow - association.CreatedAt > _expirationTime)
            {
                _linkCodes.TryRemove(linkCode, out _);
                return null;
            }

            return association;
        }

        return null;
    }

    private void CleanupExpiredCodes()
    {
        var cutoff = DateTime.UtcNow.Add(-_expirationTime);
        foreach (var kvp in _linkCodes)
        {
            if (kvp.Value != null && kvp.Value.CreatedAt < cutoff)
            {
                _linkCodes.TryRemove(kvp.Key, out _);
            }
        }
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }

        return new string(result);
    }
}
