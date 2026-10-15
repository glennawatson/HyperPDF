// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// Interns the names of one document. Known names resolve through a shared perfect hash; names the library does not know
/// are added to this table on first sight. The added spellings sit one after another in a single array, found through an
/// open-addressing table of positions. Published spellings remain readable when the table grows.
/// Safe to call from any thread.
/// </summary>
[DebuggerDisplay("PdfNameTable: {Count} names")]
public sealed class PdfNameTable
{
    /// <summary>The largest UTF-8 encoding of a name given as text: four bytes per character.</summary>
    private const int MaxEncodedName = PdfLimits.MaxNameLength * 4;

    /// <summary>The names the offsets table starts with room for.</summary>
    private const int InitialNames = 16;

    /// <summary>The bytes the spelling array starts with.</summary>
    private const int InitialBytes = 256;

    /// <summary>The slots of the position table at first; a power of two.</summary>
    private const int InitialSlots = 32;

    /// <summary>The factor by which the arrays grow.</summary>
    private const int GrowthFactor = 2;

    /// <summary>Serialises additions, created only when the document introduces a name.</summary>
    private Lock? _gate;

    /// <summary>The spellings of the added names one after another.</summary>
    private byte[] _bytes = [];

    /// <summary>Where each added name starts in <see cref="_bytes"/>, and one more for where the last ends.</summary>
    private int[] _starts = [];

    /// <summary>The position table: zero for a free slot, else one more than the index of the added name.</summary>
    private int[] _slots = [];

    /// <summary>The number of added names whose spelling and slot are published.</summary>
    private int _count;

    /// <summary>The bytes of <see cref="_bytes"/> in use; guarded by the gate.</summary>
    private int _used;

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

        var hash = unchecked((uint)ByteArrayComparer.Instance.GetHashCode(spelling));
        return TryFind(spelling, hash, out var id) ? new(id) : AddUnknown(spelling, hash);
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
        if ((uint)index >= (uint)Volatile.Read(ref _count))
        {
            return default;
        }

        // A name is published after its spelling, so the arrays read next hold it.
        var starts = Volatile.Read(ref _starts);
        var bytes = Volatile.Read(ref _bytes);
        return (uint)(index + 1) < (uint)starts.Length && starts[index + 1] <= bytes.Length ? bytes.AsSpan(starts[index], starts[index + 1] - starts[index]) : default;
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

    /// <summary>Puts a name's entry in the first free slot at or after its hash.</summary>
    /// <param name="slots">The position table, whose length is a power of two.</param>
    /// <param name="hash">The hash of the name's spelling.</param>
    /// <param name="entry">One more than the index of the added name.</param>
    private static void Insert(int[] slots, uint hash, int entry)
    {
        var mask = slots.Length - 1;
        var slot = (int)(hash & (uint)mask);
        while (slots[slot] != 0)
        {
            slot = (slot + 1) & mask;
        }

        Volatile.Write(ref slots[slot], entry);
    }

    /// <summary>Finds an added name.</summary>
    /// <param name="spelling">The name's bytes.</param>
    /// <param name="hash">The hash of the bytes.</param>
    /// <param name="id">The name's id.</param>
    /// <returns><see langword="true"/> when the name was added.</returns>
    private bool TryFind(ReadOnlySpan<byte> spelling, uint hash, out int id)
    {
        var slots = Volatile.Read(ref _slots);
        if (slots.Length > 0)
        {
            var mask = slots.Length - 1;
            for (var slot = (int)(hash & (uint)mask); Volatile.Read(ref slots[slot]) is var entry && entry != 0; slot = (slot + 1) & mask)
            {
                id = KnownNameSpellings.Count + entry - 1;
                if (GetSpelling(new(id)).SequenceEqual(spelling))
                {
                    return true;
                }
            }
        }

        id = 0;
        return false;
    }

    /// <summary>Adds a spelling, publishing it before its slot so lock-free readers that find the slot also find the spelling.</summary>
    /// <param name="spelling">The spelling.</param>
    /// <param name="hash">The hash of the spelling.</param>
    /// <returns>The newly assigned name.</returns>
    private PdfName AddUnknown(ReadOnlySpan<byte> spelling, uint hash)
    {
        lock (Volatile.Read(ref _gate) ?? CreateGate())
        {
            if (TryFind(spelling, hash, out var existing))
            {
                return new(existing);
            }

            var count = _count;
            EnsureRoom(count, spelling.Length);
            spelling.CopyTo(_bytes.AsSpan(_used));
            _used += spelling.Length;
            _starts[count + 1] = _used;
            Volatile.Write(ref _count, count + 1);
            Insert(_slots, hash, count + 1);
            return new(KnownNameSpellings.Count + count);
        }
    }

    /// <summary>Creates the insertion lock once.</summary>
    /// <returns>The shared lock.</returns>
    private Lock CreateGate()
    {
        var created = new Lock();
        return Interlocked.CompareExchange(ref _gate, created, null) ?? created;
    }

    /// <summary>Makes sure the arrays have room for one more name and its spelling.</summary>
    /// <param name="count">The names added so far.</param>
    /// <param name="length">The bytes in the new spelling.</param>
    private void EnsureRoom(int count, int length)
    {
        if (_starts.Length == 0)
        {
            _starts = new int[InitialNames];
            _starts[0] = 0;
            _bytes = new byte[Math.Max(InitialBytes, length)];
            _slots = new int[InitialSlots];
            _used = 0;
        }

        if (count + 1 >= _starts.Length)
        {
            var larger = new int[_starts.Length * GrowthFactor];
            _starts.AsSpan(0, count + 1).CopyTo(larger);
            Volatile.Write(ref _starts, larger);
        }

        if (_used + length > _bytes.Length)
        {
            var larger = new byte[Math.Max(_bytes.Length * GrowthFactor, _used + length)];
            _bytes.AsSpan(0, _used).CopyTo(larger);
            Volatile.Write(ref _bytes, larger);
        }

        if ((count + 1) * GrowthFactor > _slots.Length)
        {
            Rehash(count);
        }
    }

    /// <summary>Moves the slots into a table twice the size.</summary>
    /// <param name="count">The names added so far.</param>
    private void Rehash(int count)
    {
        var larger = new int[_slots.Length * GrowthFactor];
        for (var i = 0; i < count; i++)
        {
            Insert(larger, unchecked((uint)ByteArrayComparer.Instance.GetHashCode(_bytes.AsSpan(_starts[i], _starts[i + 1] - _starts[i]))), i + 1);
        }

        Volatile.Write(ref _slots, larger);
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
}
