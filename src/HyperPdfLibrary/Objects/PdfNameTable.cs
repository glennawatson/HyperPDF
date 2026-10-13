// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// Interns the names of one document. Known names resolve through a shared perfect hash; names the library does not know
/// are added to this table on first sight. Safe to call from any thread.
/// </summary>
[DebuggerDisplay("PdfNameTable: {Count} names")]
public sealed class PdfNameTable
{
    /// <summary>The largest UTF-8 encoding of a name given as text: four bytes per character.</summary>
    private const int MaxEncodedName = PdfLimits.MaxNameLength * 4;

    /// <summary>The initial capacity for document-specific spellings.</summary>
    private const int InitialCapacity = 16;

    /// <summary>The factor by which spelling storage grows.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The lookup and lock for the names this document added; made with the first such name.</summary>
    private UnknownNames? _unknown;

    /// <summary>The spellings of the names this document added, indexed by id minus the known count.</summary>
    private byte[]?[] _spellings = [];

    /// <summary>The number of published document-specific spellings.</summary>
    private int _count;

    /// <summary>Gets the number of names, known and added.</summary>
    public int Count => KnownNameSpellings.Count + Volatile.Read(ref _count);

    /// <summary>Gets the spelling of a known name.</summary>
    /// <param name="name">The known name.</param>
    /// <returns>The UTF-8 bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> GetKnownSpelling(KnownName name)
    {
        var offsets = KnownNameSpellings.Offsets;
        var index = (int)name;
        return KnownNameSpellings.Blob.Slice(offsets[index], offsets[index + 1] - offsets[index]);
    }

    /// <summary>Finds a known name by its bytes.</summary>
    /// <param name="spelling">The name's bytes, without the slash and with #xx escapes decoded.</param>
    /// <param name="name">The known name.</param>
    /// <returns><see langword="true"/> when the name is known.</returns>
    public static bool TryGetKnown(ReadOnlySpan<byte> spelling, out PdfName name)
    {
        if (!spelling.IsEmpty)
        {
            var hash = KnownNameHash.Hash(spelling);
            var size = KnownNameSpellings.Count - 1;
            var displacement = KnownNameSpellings.Displacements[(int)(hash % (uint)size)];
            var slot = displacement < 0 ? -displacement - 1 : (int)(KnownNameHash.Displace(hash, displacement) % (uint)size);
            var id = KnownNameSpellings.Slots[slot];
            if (GetKnownSpelling((KnownName)id).SequenceEqual(spelling))
            {
                name = new(id);
                return true;
            }
        }

        name = default;
        return false;
    }

    /// <summary>Interns a name.</summary>
    /// <param name="spelling">The name's bytes, without the slash and with #xx escapes decoded.</param>
    /// <returns>The name.</returns>
    public PdfName Intern(ReadOnlySpan<byte> spelling)
    {
        if (TryGetKnown(spelling, out var known))
        {
            return known;
        }

        var unknown = Volatile.Read(ref _unknown);
        if (unknown is not null && unknown.Ids.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(spelling, out var id))
        {
            return new(id);
        }

        unknown ??= CreateUnknown();
        lock (unknown.Gate)
        {
            return unknown.Ids.GetAlternateLookup<ReadOnlySpan<byte>>().TryGetValue(spelling, out id) ? new(id) : AddUnknown(unknown, spelling);
        }
    }

    /// <summary>Interns a name given as text.</summary>
    /// <param name="spelling">The name, without the slash.</param>
    /// <returns>The name.</returns>
    public PdfName Intern(string spelling)
    {
        ArgumentNullException.ThrowIfNull(spelling);
        return InternEncoded(spelling);
    }

    /// <summary>Gets the spelling of a name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The UTF-8 bytes, or empty when the name is not from this table.</returns>
    public ReadOnlySpan<byte> GetSpelling(PdfName name)
    {
        if (name.IsKnown)
        {
            return GetKnownSpelling((KnownName)name.Id);
        }

        var index = name.Id - KnownNameSpellings.Count;
        var spellings = Volatile.Read(ref _spellings);
        return (uint)index < (uint)spellings.Length ? Volatile.Read(ref spellings[index]) ?? [] : [];
    }

    /// <summary>Gets the spelling of a name as text.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The name decoded as UTF-8.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetString(PdfName name) => Encoding.UTF8.GetString(GetSpelling(name));

    /// <summary>Determines whether a name is spelled a certain way.</summary>
    /// <param name="name">The name.</param>
    /// <param name="spelling">The expected bytes.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameEquals(PdfName name, ReadOnlySpan<byte> spelling) => GetSpelling(name).SequenceEqual(spelling);

    /// <summary>Publishes a new spelling before making its id visible to lock-free readers.</summary>
    /// <param name="unknown">The lookup of the document-specific names.</param>
    /// <param name="spelling">The document-specific spelling.</param>
    /// <returns>The newly assigned name.</returns>
    private PdfName AddUnknown(UnknownNames unknown, ReadOnlySpan<byte> spelling)
    {
        var count = _count;
        var spellings = _spellings;
        if (count == spellings.Length)
        {
            var grown = new byte[]?[Math.Max(InitialCapacity, spellings.Length * GrowthFactor)];
            spellings.CopyTo(grown, 0);
            spellings = grown;
            Volatile.Write(ref _spellings, spellings);
        }

        var bytes = spelling.ToArray();
        var id = KnownNameSpellings.Count + count;
        Volatile.Write(ref spellings[count], bytes);
        Volatile.Write(ref _count, count + 1);

        // Readers that obtain the id must also see its spelling and a Count that includes it.
        _ = unknown.Ids.TryAdd(bytes, id);
        return new(id);
    }

    /// <summary>Makes the lookup of the document-specific names once.</summary>
    /// <returns>The lookup.</returns>
    private UnknownNames CreateUnknown()
    {
        var created = new UnknownNames();
        return Interlocked.CompareExchange(ref _unknown, created, null) ?? created;
    }

    /// <summary>Encodes a spelling as UTF-8 and interns it.</summary>
    /// <param name="spelling">The name, without the slash.</param>
    /// <returns>The name.</returns>
    private PdfName InternEncoded(string spelling)
    {
        Span<byte> buffer = stackalloc byte[MaxEncodedName];
        var length = Encoding.UTF8.GetBytes(spelling.AsSpan(0, Math.Min(spelling.Length, PdfLimits.MaxNameLength)), buffer);
        return Intern(buffer[..Math.Min(length, PdfLimits.MaxNameLength)]);
    }

    /// <summary>The lookup of the names a document added and the lock that serialises adding them.</summary>
    private sealed class UnknownNames
    {
        /// <summary>Gets the lock that serialises insertion of a new document-specific name.</summary>
        internal Lock Gate { get; } = new();

        /// <summary>Gets the ids of the names this document added; the gate serialises all writers.</summary>
        internal ConcurrentDictionary<byte[], int> Ids { get; } = new(1, InitialCapacity, ByteArrayComparer.Instance);
    }
}
