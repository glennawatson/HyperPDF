// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace HyperPdf.Http;

/// <summary>Reuses a SHA-256 context within one download service's lifetime.</summary>
internal sealed class DownloadHashes : IDisposable
{
    /// <summary>The bytes in a SHA-256 result.</summary>
    private const int HashBytes = 32;

    /// <summary>Serializes access to the reusable context.</summary>
    private readonly Lock _gate = new();

    /// <summary>The available context, or null while in use.</summary>
    private IncrementalHash? _ready;

    /// <summary>Whether the owning service has ended.</summary>
    private bool _disposed;

    /// <inheritdoc/>
    public void Dispose()
    {
        IncrementalHash? hash;
        lock (_gate)
        {
            _disposed = true;
            hash = _ready;
            _ready = null;
        }

        hash?.Dispose();
    }

    /// <summary>Takes a context, making another when a concurrent operation already uses the retained one.</summary>
    /// <returns>The context; return it when the operation finishes.</returns>
    internal IncrementalHash Take()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var hash = _ready;
            _ready = null;
            return hash ?? IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        }
    }

    /// <summary>Clears a finished or interrupted hash and retains one context for the next operation.</summary>
    /// <param name="hash">The context taken by the operation.</param>
    internal void Return(IncrementalHash hash)
    {
        Span<byte> digest = stackalloc byte[HashBytes];
        _ = hash.GetHashAndReset(digest);
        lock (_gate)
        {
            if (!_disposed && _ready is null)
            {
                _ready = hash;
                return;
            }
        }

        hash.Dispose();
    }
}
