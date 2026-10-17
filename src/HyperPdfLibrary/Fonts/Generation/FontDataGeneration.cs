// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>Builds requested engine CMap resources from compact binary CMap data.</summary>
internal static class FontDataGeneration
{
    /// <summary>The pinned PDF.js binary resource revision.</summary>
    private const string ResourceRevision = "f5e56f0af970e1fe5eb8faa9eb3212281eb97d07";

    /// <summary>The longest supported inheritance chain.</summary>
    private const int MaxParents = 16;

    /// <summary>Finds the supported collection of a resource.</summary>
    /// <param name="name">The resource name.</param>
    /// <returns>The collection, or null.</returns>
    internal static Collection? FindCollection(string name)
    {
        foreach (var collection in Collections.All)
        {
            if (name == collection.UnicodeTable || Array.IndexOf(collection.CMaps, name) >= 0)
            {
                return collection;
            }
        }

        return null;
    }

    /// <summary>Builds one engine CMap and the base maps it needs.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="name">The map name.</param>
    /// <param name="collection">The owning collection.</param>
    /// <param name="cancellationToken">Cancels resource I/O and generation.</param>
    /// <returns>The compressed engine table.</returns>
    internal static async ValueTask<byte[]> CMapAsync(PdfFontResources resources, string name, Collection collection, CancellationToken cancellationToken)
    {
        var maps = new Dictionary<string, BinaryCMapData>(StringComparer.Ordinal);
        await ReadMapAsync(resources, name, collection, maps, cancellationToken).ConfigureAwait(false);
        return PackCMap(maps, name, collection, cancellationToken);
    }

    /// <summary>Builds the requested collection's Unicode table from its binary mapping.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="cancellationToken">Cancels resource I/O and generation.</param>
    /// <returns>The compressed table.</returns>
    internal static async ValueTask<byte[]> UnicodeAsync(PdfFontResources resources, Collection collection, CancellationToken cancellationToken)
    {
        var data = await ReadBinaryAsync(resources, collection.UnicodeTable, cancellationToken).ConfigureAwait(false);
        var values = BinaryCMapReader.Parse(data, cancellationToken).Unicode;
        var inverseName = Array.Find(collection.CMaps, static name => name.EndsWith("UTF32-H", StringComparison.Ordinal))!;
        var inverse = await ReadBinaryAsync(resources, inverseName, cancellationToken).ConfigureAwait(false);
        return PackUnicode(values, BinaryCMapReader.Parse(inverse, cancellationToken).Ranges, cancellationToken);
    }

    /// <summary>Reads and validates the binary CMap inheritance chain.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="name">The map name.</param>
    /// <param name="collection">The owning collection.</param>
    /// <param name="maps">The mappings read in this chain.</param>
    /// <param name="cancellationToken">Cancels resource I/O.</param>
    /// <returns>A task completing when the mappings are available.</returns>
    /// <exception cref="InvalidDataException">The resource inheritance chain is invalid.</exception>
    private static async ValueTask ReadMapAsync(PdfFontResources resources, string name, Collection collection, Dictionary<string, BinaryCMapData> maps, CancellationToken cancellationToken)
    {
        if (maps.Count >= MaxParents || maps.ContainsKey(name) || name.Contains('/') || name.Contains('\\') || name.Contains('.'))
        {
            throw new InvalidDataException("The binary CMap has an invalid inheritance chain.");
        }

        var data = await ReadBinaryAsync(resources, name, cancellationToken).ConfigureAwait(false);
        var map = BinaryCMapReader.Parse(data, cancellationToken);
        maps.Add(name, map);
        if (map.Parent.Length == 0)
        {
            return;
        }

        await ReadMapAsync(resources, map.Parent, collection, maps, cancellationToken).ConfigureAwait(false);
        await FontDataResources.EnsureAsync(
            resources.Data,
            "CMaps",
            $"{map.Parent}.bin",
            token => ValueTask.FromResult(PackCMap(maps, map.Parent, collection, token)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Packs and compresses one CMap with an explicit token.</summary>
    /// <param name="maps">The parsed inheritance chain.</param>
    /// <param name="name">The requested CMap.</param>
    /// <param name="collection">The owning collection.</param>
    /// <param name="cancellationToken">Cancels packing.</param>
    /// <returns>The compressed table.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte[] PackCMap(Dictionary<string, BinaryCMapData> maps, string name, Collection collection, CancellationToken cancellationToken) =>
        Compress(PackedTables.PackCMap(maps, name, collection, cancellationToken), cancellationToken);

    /// <summary>Fills, packs and compresses Unicode mappings with an explicit token.</summary>
    /// <param name="values">The explicit CID mappings.</param>
    /// <param name="ranges">The fallback Unicode ranges.</param>
    /// <param name="cancellationToken">Cancels generation.</param>
    /// <returns>The compressed table.</returns>
    private static byte[] PackUnicode(int[] values, CidRange[] ranges, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rented = ArrayPool<int>.Shared.Rent(BinaryCMapReader.MaxItems);
        try
        {
            var table = rented.AsSpan(0, BinaryCMapReader.MaxItems);
            table.Clear();
            values.CopyTo(table);
            BinaryUnicodeFallback.Fill(table, ranges, cancellationToken);
            return Compress(PackedTables.PackUnicode(table, cancellationToken), cancellationToken);
        }
        finally
        {
            ArrayPool<int>.Shared.Return(rented);
        }
    }

    /// <summary>Reads a compact binary mapping from the resource pack.</summary>
    /// <param name="resources">The selected font resources.</param>
    /// <param name="name">The mapping name.</param>
    /// <param name="cancellationToken">Cancels resource I/O.</param>
    /// <returns>The binary mapping.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ValueTask<byte[]> ReadBinaryAsync(PdfFontResources resources, string name, CancellationToken cancellationToken) =>
        FontDataDownload.ReadAsync(resources.Data, $"mozilla/pdf.js/{ResourceRevision}/external/bcmaps/{name}.bcmap", cancellationToken);

    /// <summary>Compresses a generated table through the engine's codec.</summary>
    /// <param name="data">The table bytes.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] Compress(byte[] data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var output = new PooledBuffer(0);
        try
        {
            ZLibCodec.Compress(data, ref output);
            cancellationToken.ThrowIfCancellationRequested();
            return output.WrittenSpan.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }
}
