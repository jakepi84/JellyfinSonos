using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.JellyfinSonos.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyfinSonos.Services;

#pragma warning disable CS1591 // XML docs not required for plugin-internal helpers
/// <summary>
/// Lightweight OAuth helper for Sonos (authorization code + refresh + signed access tokens).
/// </summary>
public class OAuthService
{
    private const int AccessTokenMinutes = 60;
    private const int AuthorizationCodeMinutes = 10;
    private const int RefreshTokenDays = 30;

    private readonly ConcurrentDictionary<string, AuthCode> _authCodes = new();
    private readonly ConcurrentDictionary<string, RefreshTokenInfo> _refreshTokens = new();
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(ILogger<OAuthService> logger)
    {
        _logger = logger;
    }

    private PluginConfiguration Config => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    /// <summary>
    /// Create a short-lived authorization code bound to the client and redirect URI.
    /// </summary>
    public string CreateAuthorizationCode(Guid userId, string username, string clientId, string redirectUri, string? codeChallenge, string? codeChallengeMethod)
    {
        var code = GenerateTokenString(32);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(AuthorizationCodeMinutes);

        _authCodes[code] = new AuthCode
        {
            UserId = userId,
            Username = username,
            ClientId = clientId,
            RedirectUri = redirectUri,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = string.IsNullOrWhiteSpace(codeChallengeMethod) ? "plain" : codeChallengeMethod,
            ExpiresAt = expiresAt
        };

        Cleanup();
        return code;
    }

    /// <summary>
    /// Redeem an authorization code, validating PKCE if provided.
    /// </summary>
    public bool TryRedeemCode(string code, string clientId, string redirectUri, string? codeVerifier, out AuthCode? authCode)
    {
        authCode = null;

        if (!_authCodes.TryRemove(code, out var stored))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow > stored.ExpiresAt)
        {
            return false;
        }

        if (!string.Equals(clientId, stored.ClientId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(redirectUri, stored.RedirectUri, StringComparison.Ordinal))
        {
            return false;
        }

        if (!ValidatePkce(stored, codeVerifier))
        {
            return false;
        }

        authCode = stored;
        return true;
    }

    /// <summary>
    /// Issue an access token for the given principal.
    /// </summary>
    public AccessTokenResult IssueAccessToken(Guid userId, string username, string scope)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(AccessTokenMinutes);
        var payload = $"{userId}|{username}|{expiresAt.ToUnixTimeSeconds()}|{scope}";
        var signature = ComputeSignature(payload);

        var token = $"{Base64UrlEncode(Encoding.UTF8.GetBytes(payload))}.{Base64UrlEncode(signature)}";

        return new AccessTokenResult(token, expiresAt, scope);
    }

    /// <summary>
    /// Issue a refresh token (stored server-side for revocation/rotation).
    /// </summary>
    public RefreshTokenResult IssueRefreshToken(Guid userId, string username, string scope)
    {
        var token = GenerateTokenString(48);
        var expiresAt = DateTimeOffset.UtcNow.AddDays(RefreshTokenDays);

        _refreshTokens[token] = new RefreshTokenInfo
        {
            UserId = userId,
            Username = username,
            Scope = scope,
            ExpiresAt = expiresAt
        };

        Cleanup();
        return new RefreshTokenResult(token, expiresAt, scope);
    }

    /// <summary>
    /// Validate an access token signature and expiry.
    /// </summary>
    public bool ValidateAccessToken(string? token, out OAuthPrincipal principal)
    {
        principal = default!;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return false;
        }

        var payload = Encoding.UTF8.GetString(Base64UrlDecode(parts[0]));
        var providedSig = Base64UrlDecode(parts[1]);
        var expectedSig = ComputeSignature(payload);

        if (!CryptographicOperations.FixedTimeEquals(providedSig, expectedSig))
        {
            return false;
        }

        var pieces = payload.Split('|');
        if (pieces.Length < 4)
        {
            return false;
        }

        if (!Guid.TryParse(pieces[0], out var userId))
        {
            return false;
        }

        var username = pieces[1];

        if (!long.TryParse(pieces[2], out var expUnix))
        {
            return false;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expUnix);
        if (DateTimeOffset.UtcNow > expiresAt)
        {
            return false;
        }

        var scope = pieces[3];
        principal = new OAuthPrincipal(userId, username, scope, expiresAt);
        return true;
    }

    /// <summary>
    /// Exchange a refresh token for a new access token (and rotated refresh token).
    /// </summary>
    public bool TryExchangeRefreshToken(string refreshToken, out AccessTokenResult accessToken, out RefreshTokenResult newRefreshToken)
    {
        accessToken = default!;
        newRefreshToken = default!;

        if (!_refreshTokens.TryRemove(refreshToken, out var stored))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow > stored.ExpiresAt)
        {
            return false;
        }

        accessToken = IssueAccessToken(stored.UserId, stored.Username, stored.Scope);
        newRefreshToken = IssueRefreshToken(stored.UserId, stored.Username, stored.Scope);
        return true;
    }

    private byte[] ComputeSignature(string payload)
    {
        var key = GetSigningKey();
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }

    private byte[] GetSigningKey()
    {
        var secret = string.IsNullOrWhiteSpace(Config.SecretKey)
            ? "change-me-jellyfin-sonos"
            : Config.SecretKey;

        return Encoding.UTF8.GetBytes(secret.PadRight(32, '0').Substring(0, 32));
    }

    private static string GenerateTokenString(int length)
    {
        var buffer = RandomNumberGenerator.GetBytes(length);
        return Base64UrlEncode(buffer);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var normalized = input.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2:
                normalized += "==";
                break;
            case 3:
                normalized += "=";
                break;
        }

        return Convert.FromBase64String(normalized);
    }

    private static bool ValidatePkce(AuthCode stored, string? codeVerifier)
    {
        if (string.IsNullOrWhiteSpace(stored.CodeChallenge))
        {
            return true; // no PKCE
        }

        if (string.IsNullOrWhiteSpace(codeVerifier))
        {
            return false;
        }

        var method = stored.CodeChallengeMethod?.ToLowerInvariant() ?? "plain";
        string computed;

        if (method == "s256")
        {
            using var sha = SHA256.Create();
            computed = Base64UrlEncode(sha.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier)));
        }
        else
        {
            computed = codeVerifier;
        }

        return string.Equals(computed, stored.CodeChallenge, StringComparison.Ordinal);
    }

    private void Cleanup()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var kvp in _authCodes.ToList())
        {
            if (kvp.Value.ExpiresAt < now)
            {
                _authCodes.TryRemove(kvp.Key, out _);
            }
        }

        foreach (var kvp in _refreshTokens.ToList())
        {
            if (kvp.Value.ExpiresAt < now)
            {
                _refreshTokens.TryRemove(kvp.Key, out _);
            }
        }
    }

    public record AccessTokenResult(string Token, DateTimeOffset ExpiresAt, string Scope);

    public record RefreshTokenResult(string Token, DateTimeOffset ExpiresAt, string Scope);

    public record OAuthPrincipal(Guid UserId, string Username, string Scope, DateTimeOffset ExpiresAt);

    public class AuthCode
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string? CodeChallenge { get; set; }
        public string? CodeChallengeMethod { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    private class RefreshTokenInfo
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Scope { get; set; } = "smapi";
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
#pragma warning restore CS1591
