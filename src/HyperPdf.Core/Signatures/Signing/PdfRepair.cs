// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>
/// Rebuilds the cross-reference information of a damaged PDF by scanning it for objects, as PDF readers do,
/// so documents with wrong offsets, broken tables or broken cross-reference streams can still be signed.
/// </summary>
internal static class PdfRepair
{
    /// <summary>Checks every in-use entry of a parsed structure points at the object it names.</summary>
    /// <param name="structure">The structure read from the cross-reference sections.</param>
    /// <returns><see langword="true"/> when the cross-reference information can be trusted.</returns>
    internal static bool IsConsistent(PdfStructure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        var file = structure.File.Span;
        foreach (var (number, entry) in structure.Entries)
        {
            if (entry.Type == XrefEntry.InUse && (entry.Location < 0 || entry.Location >= file.Length || ObjectNumberAt(file, (int)entry.Location) != number))
            {
                return false;
            }

            if (entry.Type == XrefEntry.Compressed && (!structure.Entries.TryGetValue((int)entry.Location, out var container) || container.Type != XrefEntry.InUse))
            {
                return false;
            }
        }

        return PdfSyntax.FindKey(structure.Trailer, 0, "Root"u8) >= 0;
    }

    /// <summary>Rebuilds a structure by scanning the whole file for <c>N G obj</c> headers.</summary>
    /// <param name="memory">The file.</param>
    /// <returns>The rebuilt structure, marked as repaired.</returns>
    /// <exception cref="InvalidDataException">No catalog can be found.</exception>
    internal static PdfStructure Rebuild(ReadOnlyMemory<byte> memory)
    {
        var file = memory.Span;
        var entries = new Dictionary<int, XrefEntry>();
        var catalog = -1;
        for (var i = 0; i < file.Length; i++)
        {
            if (!IsTokenStart(file, i) || ObjectNumberAt(file, i) is not (>= 0 and var number))
            {
                continue;
            }

            // Later definitions replace earlier ones, as an incremental update would.
            entries[number] = new(XrefEntry.InUse, i, 0);
            catalog = IsType(file, i, "Catalog"u8) ? number : catalog;
        }

        var structure = PdfReader.Structure(memory, entries, FindTrailer(file, catalog), -1, true);
        AddCompressedObjects(structure);
        return structure;
    }

    /// <summary>Finds the trailer: the last <c>trailer</c> dictionary naming a catalog, or one made for the catalog found.</summary>
    /// <param name="file">The file.</param>
    /// <param name="catalog">The catalog's object number, or -1.</param>
    /// <returns>The trailer dictionary.</returns>
    /// <exception cref="InvalidDataException">Neither a trailer nor a catalog exists.</exception>
    private static byte[] FindTrailer(ReadOnlySpan<byte> file, int catalog)
    {
        var end = file.Length;
        while (end > 0 && file[..end].LastIndexOf("trailer"u8) is var at and >= 0)
        {
            var dictionary = PdfSyntax.SkipSpace(file, at + "trailer"u8.Length);
            var trailer = file[dictionary..PdfSyntax.ValueEnd(file, dictionary)];
            if (trailer.Length > 1 && PdfSyntax.FindKey(trailer, 0, "Root"u8) >= 0)
            {
                return trailer.ToArray();
            }

            end = at;
        }

        return catalog >= 0
            ? Encoding.ASCII.GetBytes(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"<< /Root {catalog} 0 R >>"))
            : throw new InvalidDataException("The file is too damaged to sign: it has no catalog.");
    }

    /// <summary>Adds the objects packed into object streams, unless a plain object of the same number exists.</summary>
    /// <param name="structure">The structure being rebuilt.</param>
    private static void AddCompressedObjects(PdfStructure structure)
    {
        var file = structure.File.Span;
        var containers = new List<int>();
        foreach (var (number, entry) in structure.Entries)
        {
            if (IsType(file, (int)entry.Location, "ObjStm"u8))
            {
                containers.Add(number);
            }
        }

        foreach (var container in containers)
        {
            byte[] stream;
            try
            {
                stream = PdfReader.GetObjectStream(structure, container);
            }
            catch (InvalidDataException)
            {
                continue;
            }
            catch (NotSupportedException)
            {
                continue;
            }

            var header = 0;
            for (var index = 0; header >= 0; index++)
            {
                header = PdfSyntax.ReadLong(stream, PdfSyntax.SkipSpace(stream, header), out var number);
                header = header < 0 ? -1 : PdfSyntax.ReadLong(stream, PdfSyntax.SkipSpace(stream, header), out _);
                if (header >= 0)
                {
                    _ = structure.Entries.TryAdd((int)number, new(XrefEntry.Compressed, container, index));
                }
            }
        }
    }

    /// <summary>Reads the object number of an <c>N G obj</c> header.</summary>
    /// <param name="file">The file.</param>
    /// <param name="offset">Where the header should start.</param>
    /// <returns>The number, or -1 when there is no header there.</returns>
    private static int ObjectNumberAt(ReadOnlySpan<byte> file, int offset)
    {
        var index = PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, offset), out var number);
        index = index < 0 || index >= file.Length || !PdfSyntax.IsSpace(file[index]) ? -1 : PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, index), out _);
        index = index < 0 ? -1 : PdfSyntax.SkipSpace(file, index);
        var isHeader = index >= 0 && file[index..].StartsWith("obj"u8) && (index + "obj"u8.Length >= file.Length || !char.IsAsciiLetterOrDigit((char)file[index + "obj"u8.Length]));
        return isHeader && number <= int.MaxValue ? (int)number : -1;
    }

    /// <summary>Determines whether a number could start at an index: a digit after white space or a delimiter.</summary>
    /// <param name="file">The file.</param>
    /// <param name="index">The index.</param>
    /// <returns><see langword="true"/> when a token starts there.</returns>
    private static bool IsTokenStart(ReadOnlySpan<byte> file, int index) =>
        char.IsAsciiDigit((char)file[index]) && (index == 0 || PdfSyntax.IsSpace(file[index - 1]) || PdfSyntax.IsDelimiter(file[index - 1]));

    /// <summary>Determines whether the object at an offset is a dictionary whose <c>/Type</c> is a name.</summary>
    /// <param name="file">The file.</param>
    /// <param name="offset">The object's offset.</param>
    /// <param name="type">The type name without its slash.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsType(ReadOnlySpan<byte> file, int offset, ReadOnlySpan<byte> type)
    {
        var index = PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, offset), out _);
        index = index < 0 ? -1 : PdfSyntax.ReadLong(file, PdfSyntax.SkipSpace(file, index), out _);
        index = index < 0 ? -1 : PdfSyntax.SkipSpace(file, PdfSyntax.SkipSpace(file, index) + "obj"u8.Length);
        if (index < 0 || index + 1 >= file.Length || file[index] != (byte)'<' || file[index + 1] != (byte)'<')
        {
            return false;
        }

        var value = PdfSyntax.FindKey(file, index, "Type"u8);
        return value >= 0 && value < file.Length && file[value] == (byte)'/' && file.Slice(value + 1, PdfSyntax.TokenEnd(file, value + 1) - value - 1).SequenceEqual(type);
    }
}
