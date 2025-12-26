using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Jellyfin.Plugin.JellyfinSonos.Services;

/// <summary>
/// Service for managing link codes used in device authorization.
/// </summary>
public class LinkCodeService
{
    private readonly ConcurrentDictionary<string, LinkCodeData> _linkCodes = new();
    private readonly TimeSpan _expirationTime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Generates a new link code.
    /// </summary>
    /// <returns>The generated link code.</returns>
    public string GenerateLinkCode()
    {
        var linkCode = GenerateRandomCode(8);
        var data = new LinkCodeData
        {
            CreatedAt = DateTime.UtcNow,
            IsUsed = false
        };
        _linkCodes.TryAdd(linkCode, data);
        
        // Clean up expired codes
        CleanupExpiredCodes();
        
        return linkCode;
    }

    /// <summary>
    /// Sets credentials for a link code.
    /// </summary>
    /// <param name="linkCode">The link code.</param>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>True if successful.</returns>
    public bool SetCredentials(string linkCode, string username, string password)
    {
        if (_linkCodes.TryGetValue(linkCode, out var data))
        {
            if (DateTime.UtcNow - data.CreatedAt > _expirationTime)
            {
                _linkCodes.TryRemove(linkCode, out _);
                return false;
            }

            data.Username = username;
            data.Password = password;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets credentials for a link code and marks it as used.
    /// </summary>
    /// <param name="linkCode">The link code.</param>
    /// <returns>Credentials if found and valid.</returns>
    public (string Username, string Password)? GetCredentials(string linkCode)
    {
        if (_linkCodes.TryGetValue(linkCode, out var data))
        {
            if (DateTime.UtcNow - data.CreatedAt > _expirationTime)
            {
                _linkCodes.TryRemove(linkCode, out _);
                return null;
            }

            if (string.IsNullOrEmpty(data.Username) || string.IsNullOrEmpty(data.Password))
            {
                return null;
            }

            data.IsUsed = true;
            _linkCodes.TryRemove(linkCode, out _); // Remove after use
            return (data.Username, data.Password);
        }

        return null;
    }

    private void CleanupExpiredCodes()
    {
        var now = DateTime.UtcNow;
        var expiredCodes = _linkCodes
            .Where(kvp => now - kvp.Value.CreatedAt > _expirationTime)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var code in expiredCodes)
        {
            _linkCodes.TryRemove(code, out _);
        }
    }

    private static string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = RandomNumberGenerator.GetBytes(length);
        var result = new char[length];
        
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random[i] % chars.Length];
        }
        
        return new string(result);
    }

    private class LinkCodeData
    {
        public DateTime CreatedAt { get; set; }
        public bool IsUsed { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
