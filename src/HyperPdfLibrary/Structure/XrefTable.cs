// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure;

/// <summary>
/// Where each object lives, as flat arrays indexed by object number: a file offset for plain objects, or the containing
/// object stream and index for compressed ones.
/// </summary>
[DebuggerDisplay("XrefTable: {Size} objects")]
internal sealed class XrefTable
{
    /// <summary>The initial capacity.</summary>
    private const int InitialCapacity = 64;

    /// <summary>The factor the capacity grows by.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The entry types.</summary>
    private byte[] _types = [];

    /// <summary>The file offsets, or the containing object stream numbers.</summary>
    private long[] _locations = [];

    /// <summary>The generations, or the indexes within object streams.</summary>
    private int[] _details = [];

    /// <summary>The objects a newer section freed, with the generation a reused number takes.</summary>
    private Dictionary<int, int>? _freed;

    /// <summary>The free entries of the section being read, which count only once the section is known not to be a hybrid.</summary>
    private List<FreeEntry>? _pendingFree;

    /// <summary>The file offsets of the object streams found while rebuilding; made with the first one.</summary>
    private List<long>? _objectStreams;

    /// <summary>Gets the file offsets of the object streams found while rebuilding, in file order.</summary>
    public ReadOnlySpan<long> ObjectStreams => CollectionsMarshal.AsSpan(_objectStreams);

    /// <summary>Gets one more than the highest object number with an entry.</summary>
    public int Size { get; private set; }

    /// <summary>Gets or sets the offset of the file header, which section and object offsets are relative to.</summary>
    public int OffsetBase { get; set; }

    /// <summary>Gets or sets the trailer dictionary.</summary>
    public PdfDictionary? Trailer { get; set; }

    /// <summary>Gets or sets the offset of the newest cross-reference section, or -1 when rebuilt.</summary>
    public long StartXref { get; set; } = -1;

    /// <summary>Gets or sets a value indicating whether the table was rebuilt by scanning a damaged file.</summary>
    public bool Repaired { get; set; }

    /// <summary>Gets or sets a value indicating whether the newest section is a cross-reference stream.</summary>
    public bool UsesXrefStreams { get; set; }

    /// <summary>Gets an entry's type.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The type; free when out of range.</returns>
    internal XrefEntryType GetType(int number) => (uint)number < (uint)Size ? (XrefEntryType)_types[number] : XrefEntryType.Free;

    /// <summary>Gets an entry's file offset or containing object stream.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The location.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal long GetLocation(int number) => _locations[number];

    /// <summary>Gets an entry's generation or index within its object stream.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The detail.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GetDetail(int number) => _details[number];

    /// <summary>Sets an entry unless a newer section already set it; free entries never block older ones.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="type">The entry type.</param>
    /// <param name="location">The file offset or containing object stream.</param>
    /// <param name="detail">The generation or index.</param>
    internal void SetIfMissing(int number, XrefEntryType type, long location, int detail)
    {
        if ((uint)number > PdfLimits.MaxObjectNumber)
        {
            return;
        }

        EnsureSize(number + 1);
        if ((XrefEntryType)_types[number] != XrefEntryType.Free || _freed?.ContainsKey(number) == true)
        {
            return;
        }

        _types[number] = (byte)type;
        _locations[number] = location;
        _details[number] = detail;
    }

    /// <summary>Notes the file offset of an object stream found while rebuilding.</summary>
    /// <param name="offset">The offset.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddObjectStream(long offset) => (_objectStreams ??= []).Add(offset);

    /// <summary>Notes a free entry of the section being read.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="generation">The generation a reused number takes.</param>
    internal void NoteFree(int number, int generation)
    {
        if (number is > 0 and <= PdfLimits.MaxObjectNumber)
        {
            (_pendingFree ??= []).Add(new(number, generation));
        }
    }

    /// <summary>Makes the noted free entries final: a newer free entry hides the object in older sections.</summary>
    internal void CommitFree()
    {
        if (_pendingFree is null)
        {
            return;
        }

        foreach (var (number, generation) in _pendingFree)
        {
            EnsureSize(number + 1);
            if ((XrefEntryType)_types[number] == XrefEntryType.Free)
            {
                _ = (_freed ??= []).TryAdd(number, generation);
            }
        }

        _pendingFree.Clear();
    }

    /// <summary>Forgets the noted free entries, as a hybrid file's classic table frees objects its stream holds.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void DiscardFree() => _pendingFree?.Clear();

    /// <summary>Gets the generation of an object a section freed.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The generation, or zero when no section freed the object.</returns>
    internal int GetFreeGeneration(int number) => _freed is not null && _freed.TryGetValue(number, out var generation) ? generation : 0;

    /// <summary>Sets an entry, replacing any existing one.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="type">The entry type.</param>
    /// <param name="location">The file offset or containing object stream.</param>
    /// <param name="detail">The generation or index.</param>
    internal void Set(int number, XrefEntryType type, long location, int detail)
    {
        if ((uint)number > PdfLimits.MaxObjectNumber)
        {
            return;
        }

        EnsureSize(number + 1);
        _types[number] = (byte)type;
        _locations[number] = location;
        _details[number] = detail;
    }

    /// <summary>Grows the arrays to hold a number of entries.</summary>
    /// <param name="size">The size needed.</param>
    internal void EnsureSize(int size)
    {
        if (size > _types.Length)
        {
            var capacity = Math.Max(Math.Max(size, _types.Length * GrowthFactor), InitialCapacity);
            Array.Resize(ref _types, capacity);
            Array.Resize(ref _locations, capacity);
            Array.Resize(ref _details, capacity);
        }

        Size = Math.Max(Size, size);
    }

    /// <summary>A free entry noted while reading a section.</summary>
    /// <param name="Number">The object number.</param>
    /// <param name="Generation">The generation a reused number takes.</param>
    private readonly record struct FreeEntry(int Number, int Generation);
}
