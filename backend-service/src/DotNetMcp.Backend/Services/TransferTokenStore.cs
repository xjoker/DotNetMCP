using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading;

namespace DotNetMcp.Backend.Services
{
    /// <summary>
    /// Transfer token for large file upload/download operations
    /// </summary>
    public class TransferToken
    {
        public string TokenId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty; // upload | download
        public string ResourceType { get; set; } = string.Empty; // resource | code | assembly
        public string? ResourceId { get; set; }
        public string? Mvid { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public bool Used { get; set; }
    }

    /// <summary>
    /// In-memory store for transfer tokens with automatic expiration
    /// </summary>
    public class TransferTokenStore
    {
        private readonly ConcurrentDictionary<string, TransferToken> _tokens = new();
        private readonly ConcurrentDictionary<string, Timer> _timers = new();

        /// <summary>
        /// Create a new transfer token
        /// </summary>
        public TransferToken Create(string operation, string resourceType, int timeoutSeconds, string? mvid = null, string? resourceId = null)
        {
            var tokenId = GenerateToken();
            var token = new TransferToken
            {
                TokenId = tokenId,
                Operation = operation,
                ResourceType = resourceType,
                ResourceId = resourceId,
                Mvid = mvid,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddSeconds(timeoutSeconds),
                Used = false
            };

            _tokens[tokenId] = token;

            // Set automatic expiration timer
            var timer = new Timer(_ =>
            {
                Revoke(tokenId);
            }, null, TimeSpan.FromSeconds(timeoutSeconds), Timeout.InfiniteTimeSpan);

            _timers[tokenId] = timer;

            return token;
        }

        /// <summary>
        /// Validate a transfer token
        /// </summary>
        public TransferToken? Validate(string tokenId)
        {
            if (!_tokens.TryGetValue(tokenId, out var token))
                return null;

            if (token.ExpiresAt < DateTime.UtcNow)
            {
                Revoke(tokenId);
                return null;
            }

            return token;
        }

        /// <summary>
        /// Mark token as used
        /// </summary>
        public void MarkUsed(string tokenId)
        {
            if (_tokens.TryGetValue(tokenId, out var token))
                token.Used = true;
        }

        /// <summary>
        /// Revoke a token immediately
        /// </summary>
        public bool Revoke(string tokenId)
        {
            if (_timers.TryRemove(tokenId, out var timer))
            {
                timer.Dispose();
            }

            return _tokens.TryRemove(tokenId, out _);
        }

        /// <summary>
        /// Get token status
        /// </summary>
        public (bool exists, bool used, int expiresIn) GetStatus(string tokenId)
        {
            if (!_tokens.TryGetValue(tokenId, out var token))
                return (false, false, 0);

            var expiresIn = (int)(token.ExpiresAt - DateTime.UtcNow).TotalSeconds;
            return (true, token.Used, Math.Max(0, expiresIn));
        }

        /// <summary>
        /// Generate a random token
        /// </summary>
        private static string GenerateToken()
        {
            var bytes = new byte[24];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
    }
}
