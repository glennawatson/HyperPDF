// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Security.Cryptography;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// The references stored for one kind of validation data and a lookup by the SHA-256 hash of the data. The arrays are
/// pooled and held until <see cref="Release"/>; a security store holds a handful of items, so the lookup is a scan.
/// </summary>
internal ref struct PdfStoredKind
{
    /// <summary>The bytes of a SHA-256 hash.</summary>
    private const int HashLength = 32;

    /// <summary>The number of hashes the first pooled hash array holds at least.</summary>
    private const int InitialHashes = 4;

    /// <summary>The factor by which the hash array grows.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The references in the store's array, in order.</summary>
    private PdfValue[]? _references;

    /// <summary>The number of references.</summary>
    private int _referenceCount;

    /// <summary>The hashes, back to back, of the items whose data is known.</summary>
    private byte[]? _hashes;

    /// <summary>The reference of each hash, in the same order.</summary>
    private PdfValue[]? _hashReferences;

    /// <summary>The number of hashes.</summary>
    private int _hashCount;

    /// <summary>Gets the references in the store's array, in order.</summary>
    internal readonly ReadOnlySpan<PdfValue> References => _references.AsSpan(0, _referenceCount);

    /// <summary>Adds a reference to the end of the array.</summary>
    /// <param name="reference">The reference.</param>
    internal void AddReference(PdfValue reference)
    {
        PooledArrays.EnsureRoom(ref _references, _referenceCount, clear: true)[_referenceCount] = reference;
        _referenceCount++;
    }

    /// <summary>Remembers the reference of an item by the hash of its data, unless the hash is already known.</summary>
    /// <param name="hash">The SHA-256 hash.</param>
    /// <param name="reference">The reference.</param>
    internal void AddHash(scoped ReadOnlySpan<byte> hash, PdfValue reference)
    {
        if (TryGetHash(hash, out _))
        {
            return;
        }

        var offset = _hashCount * HashLength;
        if (_hashes is null || _hashes.Length < offset + HashLength)
        {
            var larger = ArrayPool<byte>.Shared.Rent(Math.Max(HashLength * InitialHashes, offset * GrowthFactor));
            _hashes.AsSpan(0, offset).CopyTo(larger);
            if (_hashes is not null)
            {
                ArrayPool<byte>.Shared.Return(_hashes);
            }

            _hashes = larger;
        }

        hash.CopyTo(_hashes.AsSpan(offset));
        PooledArrays.EnsureRoom(ref _hashReferences, _hashCount, clear: true)[_hashCount] = reference;
        _hashCount++;
    }

    /// <summary>Finds the reference of an item by the hash of its data.</summary>
    /// <param name="hash">The SHA-256 hash.</param>
    /// <param name="reference">The reference, when found.</param>
    /// <returns><see langword="true"/> when the hash is known.</returns>
    internal readonly bool TryGetHash(scoped ReadOnlySpan<byte> hash, out PdfValue reference)
    {
        for (var i = 0; i < _hashCount; i++)
        {
            if (!CryptographicOperations.FixedTimeEquals(_hashes.AsSpan(i * HashLength, HashLength), hash))
            {
                continue;
            }

            reference = _hashReferences![i];
            return true;
        }

        reference = default;
        return false;
    }

    /// <summary>Gives the pooled arrays back.</summary>
    internal void Release()
    {
        PooledArrays.Return(ref _references, _referenceCount, clear: true);
        PooledArrays.Return(ref _hashReferences, _hashCount, clear: true);
        if (_hashes is not null)
        {
            ArrayPool<byte>.Shared.Return(_hashes);
            _hashes = null;
        }

        _referenceCount = 0;
        _hashCount = 0;
    }
}
