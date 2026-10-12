// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads the bytes of every object the cross-reference table lists, in the file and in object streams, and records the
/// lexical rules each breaks.
/// </summary>
internal static class PdfAFileObjects
{
    /// <summary>The most indirect objects a file may hold (ISO 32000-1, Annex C).</summary>
    private const int MaxIndirectObjects = 8_388_607;

    /// <summary>The bytes read past an object's extent, for the white space after <c>endobj</c>.</summary>
    private const int TrailingBytes = 256;

    /// <summary>The objects read between cancellation checks.</summary>
    private const int ObjectsPerCheck = 64;

    /// <summary>Reads every listed object.</summary>
    /// <param name="scan">The pass.</param>
    internal static void Check(PdfAFileScan scan)
    {
        var xref = scan.Store.XrefState;
        var packed = new Dictionary<int, List<int>>();
        var count = 0;
        for (var number = 1; number < xref.Size; number++)
        {
            if (number % ObjectsPerCheck == 0)
            {
                scan.CancellationToken.ThrowIfCancellationRequested();
            }

            switch (xref.GetType(number))
            {
                case XrefEntryType.InFile:
                {
                    count++;
                    CheckInFile(scan, number);
                    break;
                }

                case XrefEntryType.Compressed:
                {
                    count++;
                    AddPacked(packed, (int)xref.GetLocation(number), number);
                    break;
                }

                default:
                {
                    // A free entry holds no object to read.
                    break;
                }
            }
        }

        foreach (var (container, numbers) in packed)
        {
            scan.CancellationToken.ThrowIfCancellationRequested();
            CheckPacked(scan, container, numbers);
        }

        scan.Context.Require(count <= MaxIndirectObjects, PdfARule.IndirectObjectCount, PdfAFileStructureRules.FileContext);
    }

    /// <summary>Notes an object packed in an object stream.</summary>
    /// <param name="packed">The objects by object stream.</param>
    /// <param name="container">The object stream's number.</param>
    /// <param name="number">The object's number.</param>
    private static void AddPacked(Dictionary<int, List<int>> packed, int container, int number)
    {
        ref var numbers = ref CollectionsMarshal.GetValueRefOrAddDefault(packed, container, out _);
        numbers ??= [];
        numbers.Add(number);
    }

    /// <summary>Reads an object stored directly in the file.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="number">The object number.</param>
    private static void CheckInFile(PdfAFileScan scan, int number)
    {
        var store = scan.Store;
        if (!StoreExtents.TryGetExtent(store, number, out var start, out var length))
        {
            return;
        }

        // veraPDF places objects relative to the header's offset, as it places the cross-reference sections.
        var offset = start - store.XrefState.OffsetBase + store.HeaderOffset;
        if (offset <= 0 || offset >= scan.Source.Length)
        {
            return;
        }

        // One byte before the object is read too, as the object number must follow a line end, and some bytes after it,
        // as the next object's offset may point at the line end that closes this one.
        var window = (int)Math.Min(Math.Min(length + 1 + TrailingBytes, scan.Source.Length - offset + 1), int.MaxValue);
        using var lease = scan.Source.Lease(offset - 1, window);
        var reader = new PdfASyntaxReader(lease.Span, 1, scan.Buffer);
        Commit(scan, PdfAIndirectObjectSyntax.Check(ref reader, scan, number), number);
    }

    /// <summary>Reads the objects packed in one object stream.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="container">The object stream's number.</param>
    /// <param name="numbers">The numbers of the objects the table places in it.</param>
    private static void CheckPacked(PdfAFileScan scan, int container, List<int> numbers)
    {
        if (StoreReading.GetObject(scan.Store, new(container, 0)).AsStream() is not { } stream
            || stream.Dictionary.GetRaw(KnownName.N) is not { Kind: PdfKind.Integer } count
            || stream.Dictionary.GetRaw(KnownName.First) is not { Kind: PdfKind.Integer } first)
        {
            return;
        }

        var data = stream.DecodeToArray(scan.CancellationToken);
        var offsets = ReadOffsets(scan, data, count.AsInteger(), first.AsInteger());
        foreach (var number in numbers)
        {
            if (!offsets.TryGetValue(number, out var at) || at < 0 || at >= data.Length)
            {
                continue;
            }

            var reader = new PdfASyntaxReader(data, (int)at, scan.Buffer);
            Commit(scan, PdfAIndirectObjectSyntax.CheckPacked(ref reader, scan.Findings), number);
        }
    }

    /// <summary>Reads an object stream's header of object numbers and offsets.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="data">The decoded stream.</param>
    /// <param name="count">The /N value.</param>
    /// <param name="first">The /First value.</param>
    /// <returns>The offset of each object in the data.</returns>
    private static Dictionary<int, long> ReadOffsets(PdfAFileScan scan, byte[] data, long count, long first)
    {
        var offsets = new Dictionary<int, long>();
        var reader = new PdfASyntaxReader(data, 0, scan.Buffer);
        for (long i = 0; i < count && reader.Position < data.Length; i++)
        {
            reader.Next();
            var number = reader.Integer;
            reader.Next();
            if (number is > 0 and <= int.MaxValue)
            {
                offsets[(int)number] = reader.Integer + first;
            }
        }

        return offsets;
    }

    /// <summary>Records an object's findings when it could be read, otherwise drops them.</summary>
    /// <param name="scan">The pass.</param>
    /// <param name="readable">Whether the object could be read.</param>
    /// <param name="number">The object number.</param>
    private static void Commit(PdfAFileScan scan, bool readable, int number)
    {
        if (readable)
        {
            scan.Findings.CommitObject(scan.Context, number);
        }
        else
        {
            scan.Findings.Discard();
        }
    }
}
