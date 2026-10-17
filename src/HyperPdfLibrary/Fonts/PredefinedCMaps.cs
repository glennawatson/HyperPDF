// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Fonts.Generation;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// The predefined CMaps of ISO 32000-2 Table 116: Identity-H and Identity-V, and every Adobe-GB1, CNS1, Japan1 and
/// Korea1 CMap, H and V, including the UCS2, UTF16 and UTF32 ones. The CJK CMaps are generated from compact
/// binary resource packs on demand; each selected service caches parsed maps. Names the library does not know
/// read two-byte codes as CIDs, with no text of their own.
/// </summary>
internal static class PredefinedCMaps
{
    /// <summary>The format version of the packed CMaps.</summary>
    private const byte Version = 1;

    /// <summary>The flag of a vertical CMap.</summary>
    private const byte VerticalFlag = 1;

    /// <summary>The writing mode of vertical CMaps.</summary>
    private const int VerticalMode = 1;

    /// <summary>The longest code, in bytes.</summary>
    private const int MaxCodeLength = 4;

    /// <summary>The longest CMap name looked up; longer names are never predefined CMaps.</summary>
    private const int MaxNameLength = 32;

    /// <summary>The minimum encoded bytes for a CID range: code delta, length and CID delta.</summary>
    private const int MinCidRangeBytes = 3;

    /// <summary>Gets Identity-H.</summary>
    private static CompositeCMap IdentityH { get; } = new(CMap.IdentityH, CidCoding.Cid, CjkScript.None);

    /// <summary>Gets Identity-V.</summary>
    private static CompositeCMap IdentityV { get; } = new(CMap.IdentityV, CidCoding.Cid, CjkScript.None);

    /// <summary>Gets the stand-in for an unknown horizontal name.</summary>
    private static CompositeCMap UnknownH { get; } = new(CMap.IdentityH, CidCoding.Unknown, CjkScript.None);

    /// <summary>Gets the stand-in for an unknown vertical name.</summary>
    private static CompositeCMap UnknownV { get; } = new(CMap.IdentityV, CidCoding.Unknown, CjkScript.None);

    /// <summary>Generates a supported map on its first request.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="name">The map name.</param>
    /// <param name="cancellationToken">Cancels source I/O.</param>
    /// <returns>A task completing when the map is available.</returns>
    internal static ValueTask EnsureAsync(PdfFontResources resources, string name, CancellationToken cancellationToken)
    {
        var collection = FontDataGeneration.FindCollection(name);

        // The factory builds the requested map before rendering starts.
        return collection is null ? ValueTask.CompletedTask : FontDataResources.EnsureAsync(
            resources.Data,
            "CMaps",
            $"{name}.bin",
            token => FontDataGeneration.CMapAsync(resources, name, collection, token),
            cancellationToken);
    }

    /// <summary>Gets a predefined CMap by name.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="name">The CMap name.</param>
    /// <returns>The CMap; an unknown name reads two-byte codes as CIDs, vertically when it ends in V.</returns>
    internal static CompositeCMap Get(PdfFontResources resources, ReadOnlySpan<byte> name) =>
        Find(resources, name) ?? (!name.IsEmpty && name[^1] == (byte)'V' ? UnknownV : UnknownH);

    /// <summary>Finds a predefined CMap by name.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="name">The CMap name.</param>
    /// <returns>The CMap, or <see langword="null"/> when the library does not hold it.</returns>
    internal static CompositeCMap? Find(PdfFontResources resources, ReadOnlySpan<byte> name)
    {
        if (name.SequenceEqual("Identity-H"u8))
        {
            return IdentityH;
        }

        if (name.SequenceEqual("Identity-V"u8))
        {
            return IdentityV;
        }

        if (name.IsEmpty || name.Length > MaxNameLength || !Ascii.IsValid(name))
        {
            return null;
        }

        Span<char> chars = stackalloc char[MaxNameLength];
        return Find(resources, chars[..Encoding.ASCII.GetChars(name, chars)]);
    }

    /// <summary>Reads a packed CMap from disk without caching it; its base CMaps come from the cache.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="name">The CMap name.</param>
    /// <returns>The CMap, or <see langword="null"/> when the library does not hold it.</returns>
    internal static CompositeCMap? Load(PdfFontResources resources, string name)
    {
        var data = new PooledBuffer(0);
        try
        {
            return CMapResources.TryRead(name, ref data) ? Parse(resources, data.WrittenSpan) : null;
        }
        finally
        {
            data.Dispose();
        }
    }

    /// <summary>
    /// Parses a packed CMap: version, flags, coding, collection, the base CMap's name, the codespace ranges, then the
    /// CID ranges sorted by first code, each as LEB128 deltas: first code from the previous first code, length less one,
    /// and the CID as a zigzag delta from the CID the previous range would continue with.
    /// </summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="data">The decompressed resource.</param>
    /// <returns>The CMap.</returns>
    /// <exception cref="PdfException">The data is damaged.</exception>
    internal static CompositeCMap Parse(PdfFontResources resources, ReadOnlySpan<byte> data)
    {
        var reader = new PackedDataReader(data);
        if (reader.ReadByte() != Version)
        {
            throw new PdfException("The packed CMap has an unknown version.");
        }

        var vertical = (reader.ReadByte() & VerticalFlag) != 0;
        var coding = (CidCoding)reader.ReadByte();
        var collection = (CjkScript)reader.ReadByte();
        var parentName = reader.ReadBytes(reader.ReadByte());
        var parent = parentName.IsEmpty ? null : Find(resources, parentName) ?? throw new PdfException("A packed CMap's base is missing.");
        var codespaces = ReadCodespaces(ref reader);
        var cids = ReadCids(ref reader);
        return new(CMap.Create(codespaces, cids, parent?.Map, vertical ? VerticalMode : 0), coding, collection);
    }

    /// <summary>Finds a packed CMap by name, reading it on first use.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="name">The CMap name.</param>
    /// <returns>The CMap, or <see langword="null"/> when the library does not hold it.</returns>
    private static CompositeCMap? Find(PdfFontResources resources, ReadOnlySpan<char> name)
    {
        var cache = resources.CMaps;
        lock (cache.Gate)
        {
            if (cache.Cache.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(name, out var cached))
            {
                return cached;
            }

            var text = new string(name);
            var map = Load(resources, text);
            if (map is not null)
            {
                cache.Cache[text] = map;
            }

            return map;
        }
    }

    /// <summary>Reads the codespace ranges, shortest first.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The ranges.</returns>
    /// <exception cref="PdfException">A range has a bad length.</exception>
    private static CodespaceRange[] ReadCodespaces(ref PackedDataReader reader)
    {
        var codespaces = new CodespaceRange[reader.ReadByte()];
        for (var i = 0; i < codespaces.Length; i++)
        {
            int length = reader.ReadByte();
            if (length is < 1 or > MaxCodeLength)
            {
                throw new PdfException("A packed codespace range has a bad length.");
            }

            var low = reader.ReadNumber();
            codespaces[i] = new(length, low, reader.ReadNumber());
        }

        return codespaces;
    }

    /// <summary>Reads the CID ranges.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The ranges.</returns>
    /// <exception cref="PdfException">The parsed range exceeds the available data or its numeric width.</exception>
    private static CodeRangeMap ReadCids(ref PackedDataReader reader)
    {
        var encodedCount = reader.ReadNumber();
        if (encodedCount > reader.Remaining / MinCidRangeBytes)
        {
            throw new PdfException(PdfError.Format, "The packed CMap range count exceeds its data.");
        }

        var count = (int)encodedCount;
        if (count == 0)
        {
            return CodeRangeMap.Empty;
        }

        var lows = new uint[count];
        var highs = new uint[count];
        var values = new int[count];
        uint low = 0;
        long nextCid = 0;
        for (var i = 0; i < count; i++)
        {
            var nextLow = (ulong)low + reader.ReadNumber();
            var length = reader.ReadNumber();
            var cid = nextCid + reader.ReadSigned();
            if (nextLow + length > uint.MaxValue || cid < 0 || cid + length > int.MaxValue)
            {
                throw new PdfException(PdfError.Format, "The packed CMap range exceeds its code or CID width.");
            }

            low = (uint)nextLow;
            lows[i] = low;
            highs[i] = low + length;
            values[i] = (int)cid;
            nextCid = cid + length + 1;
        }

        return new(lows, highs, values);
    }
}
