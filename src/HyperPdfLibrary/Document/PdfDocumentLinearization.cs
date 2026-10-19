// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Document;

/// <summary>Reads and validates the first indirect object's linearization parameters.</summary>
public static class PdfDocumentLinearization
{
    /// <summary>The difference accepted for a /Linearized version written as a real number.</summary>
    private const double VersionTolerance = 0.000001;

    /// <summary>The number of values in a primary /H range.</summary>
    private const int PrimaryHintValues = 2;

    /// <summary>The number of values in both /H ranges.</summary>
    private const int BothHintValues = 4;

    /// <summary>Gets the linearization parameters, or null when the file has no valid first-object header.</summary>
    /// <param name="document">The open document.</param>
    /// <returns>The parameters, or null.</returns>
    public static PdfLinearizationInfo? GetInfo(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var store = document.Objects;
        if (!store.MightBeLinearized || store.WasRepaired || store.HeaderOffset != 0)
        {
            return null;
        }

        return FirstDictionary(store) is { } dictionary
            ? ReadParameters(document, dictionary)
            : null;
    }

    /// <summary>Prefetches the first file window with async I/O before reading the parameters.</summary>
    /// <param name="document">The open document.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The parameters, or null.</returns>
    public static async ValueTask<PdfLinearizationInfo?> GetInfoAsync(PdfDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();
        if (!document.Objects.MightBeLinearized)
        {
            return null;
        }

        await document.Objects.Source.PrefetchAsync(0, StoreParsing.InitialWindow, cancellationToken).ConfigureAwait(false);
        return GetInfo(document);
    }

    /// <summary>Checks whether the file's first indirect object declares linearization, even if its values are damaged.</summary>
    /// <param name="document">The open document.</param>
    /// <returns>Whether the first object has a /Linearized key.</returns>
    internal static bool IsDeclared(PdfDocument document)
    {
        var store = document.Objects;
        return store.MightBeLinearized && FirstDictionary(store)?.ContainsKey(store.Names.Intern("Linearized"u8)) == true;
    }

    /// <summary>Parses the first indirect object without relying on the cross-reference table.</summary>
    /// <param name="store">The open file.</param>
    /// <returns>The first dictionary, or null.</returns>
    private static PdfDictionary? FirstDictionary(PdfObjectStore store)
    {
        var source = store.Source;
        using var start = source.Lease(0, (int)Math.Min(source.Length, StoreParsing.InitialWindow));
        var lexer = new PdfLexer(start.Span);
        lexer.SkipLine();
        lexer.SkipWhitespace();
        return StoreParsing.TryParseObjectAt(store, lexer.Position, out _, out var value) && value.Kind == PdfKind.Dictionary
            ? value.AsDictionary()
            : null;
    }

    /// <summary>Reads the required values from a candidate dictionary.</summary>
    /// <param name="document">The document and its page tree.</param>
    /// <param name="dictionary">The first object's dictionary.</param>
    /// <returns>The validated parameters, or null.</returns>
    private static PdfLinearizationInfo? ReadParameters(PdfDocument document, PdfDictionary dictionary)
    {
        var names = document.Objects.Names;
        if (Math.Abs(dictionary.Get(names.Intern("Linearized"u8)).AsNumber() - 1) >= VersionTolerance)
        {
            return null;
        }

        var required = ReadRequired(document, dictionary, names);
        return required is null ? null : ReadHints(dictionary, names, required);
    }

    /// <summary>Checks the required file length, page tree and cross-reference values.</summary>
    /// <param name="document">The document.</param>
    /// <param name="dictionary">The parameter dictionary.</param>
    /// <param name="names">The name table.</param>
    /// <returns>The required values, or null.</returns>
    private static PdfLinearizationInfo? ReadRequired(PdfDocument document, PdfDictionary dictionary, PdfNameTable names) =>
        !TryInteger(dictionary, names, "L"u8, 1, long.MaxValue, out var fileLength)
            || fileLength != document.Objects.Source.Length
            || !TryInteger(dictionary, names, "N"u8, 1, int.MaxValue, out var pageCount)
            || pageCount != document.PageCount
            || !TryInteger(dictionary, names, "O"u8, 1, int.MaxValue, out var firstObject)
            || !TryInteger(dictionary, names, "E"u8, 1, fileLength - 1, out var firstEnd)
            || !TryInteger(dictionary, names, "T"u8, 1, fileLength - 1, out var mainXref)
            || !TryFirstPage(document, dictionary, names, pageCount, firstObject, out var pageIndex)
            ? null
            : new(fileLength, (int)pageCount, pageIndex, (int)firstObject, firstEnd, mainXref, []);

    /// <summary>Checks /P and /O against the page tree.</summary>
    /// <param name="document">The document.</param>
    /// <param name="dictionary">The parameter dictionary.</param>
    /// <param name="names">The name table.</param>
    /// <param name="pageCount">The declared page count.</param>
    /// <param name="firstObject">The declared first-page object number.</param>
    /// <param name="pageIndex">The first-page index on success.</param>
    /// <returns>Whether the first page matches the declaration.</returns>
    private static bool TryFirstPage(PdfDocument document, PdfDictionary dictionary, PdfNameTable names, long pageCount, long firstObject, out int pageIndex)
    {
        var value = dictionary.Get(names.Intern("P"u8));
        long index;
        if (value.Kind == PdfKind.Null)
        {
            index = 0;
        }
        else if (value.Kind == PdfKind.Integer)
        {
            index = value.AsInteger();
        }
        else
        {
            pageIndex = 0;
            return false;
        }

        pageIndex = (int)Math.Clamp(index, 0, int.MaxValue);
        return index >= 0 && index < pageCount && PdfDocumentPages.GetPage(document, pageIndex).Id.Number == firstObject;
    }

    /// <summary>Reads an integer without accepting truncated real values.</summary>
    /// <param name="dictionary">The parameter dictionary.</param>
    /// <param name="names">The document name table.</param>
    /// <param name="key">The parameter name.</param>
    /// <param name="minimum">The smallest accepted value.</param>
    /// <param name="maximum">The largest accepted value.</param>
    /// <param name="number">The value on success.</param>
    /// <returns>Whether the value is a bounded integer.</returns>
    private static bool TryInteger(PdfDictionary dictionary, PdfNameTable names, ReadOnlySpan<byte> key, long minimum, long maximum, out long number)
    {
        var value = dictionary.Get(names.Intern(key));
        number = value.AsInteger();
        return value.Kind == PdfKind.Integer && number >= minimum && number <= maximum;
    }

    /// <summary>Reads one or two hint ranges from /H.</summary>
    /// <param name="dictionary">The parameter dictionary.</param>
    /// <param name="names">The document name table.</param>
    /// <param name="parameters">The validated required parameters.</param>
    /// <returns>The parameters, or null when the hint ranges are invalid.</returns>
    private static PdfLinearizationInfo? ReadHints(PdfDictionary dictionary, PdfNameTable names, PdfLinearizationInfo parameters)
    {
        var hints = dictionary.GetArray(names.Intern("H"u8));
        if (hints is null)
        {
            return parameters;
        }

        if (hints.Count is not (PrimaryHintValues or BothHintValues) || !TryHintRange(hints, 0, parameters.FileLength, out var primaryOffset, out var primaryLength))
        {
            return null;
        }

        if (hints.Count == PrimaryHintValues)
        {
            return parameters with { Hints = [new(primaryOffset, primaryLength)] };
        }

        if (!TryHintRange(hints, PrimaryHintValues, parameters.FileLength, out var secondaryOffset, out var secondaryLength))
        {
            return null;
        }

        PdfHintRange[] ranges = [new(primaryOffset, primaryLength), new(secondaryOffset, secondaryLength)];
        return parameters with { Hints = ranges };
    }

    /// <summary>Checks one hint-stream range lies inside the file.</summary>
    /// <param name="hints">The /H array.</param>
    /// <param name="index">The offset's array index.</param>
    /// <param name="fileLength">The file length.</param>
    /// <param name="offset">The hint-stream offset.</param>
    /// <param name="length">The hint-stream length.</param>
    /// <returns>Whether the range is valid.</returns>
    private static bool TryHintRange(PdfArray hints, int index, long fileLength, out long offset, out long length)
    {
        var offsetValue = hints.GetRaw(index);
        var lengthValue = hints.GetRaw(index + 1);
        offset = offsetValue.AsInteger();
        length = lengthValue.AsInteger();
        return offsetValue.Kind == PdfKind.Integer && lengthValue.Kind == PdfKind.Integer
            && offset > 0 && length > 0 && offset < fileLength && length <= fileLength - offset;
    }
}
