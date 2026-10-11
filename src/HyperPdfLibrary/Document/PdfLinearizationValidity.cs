// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Document;

/// <summary>Checks file positions declared by the linearization header and page hints.</summary>
internal static class PdfLinearizationValidity
{
    /// <summary>The maximum whitespace window before the object-zero entry.</summary>
    private const int MaxXrefWhitespace = 16;

    /// <summary>Gets the free object-zero entry at the start of a classic main cross-reference table.</summary>
    private static ReadOnlySpan<byte> FreeXrefEntry => "0000000000 65535 f"u8;

    /// <summary>Checks the main xref and the declared page groups against actual object offsets.</summary>
    /// <param name="document">The opened document.</param>
    /// <param name="info">The linearization parameters.</param>
    /// <param name="ranges">The decoded page group ranges.</param>
    /// <param name="cancellationToken">Cancels the object walk.</param>
    /// <param name="reason">Describes the first fault.</param>
    /// <param name="offset">Locates the first fault.</param>
    /// <returns>Whether the declared positions agree with the file.</returns>
    internal static bool TryCheck(PdfDocument document, PdfLinearizationInfo info, PdfHintPageRange[] ranges, CancellationToken cancellationToken, out string reason, out long offset)
    {
        if (!HasMainXref(document.Objects, info.MainXrefOffset))
        {
            reason = "The linearization main cross-reference offset does not point to a cross-reference entry.";
            offset = info.MainXrefOffset;
            return false;
        }

        if (ranges.Length != info.PageCount || ranges[info.FirstPageIndex].Offset + ranges[info.FirstPageIndex].Length != info.FirstPageEndOffset)
        {
            reason = "The linearization first-page boundary does not match its page hint.";
            offset = info.FirstPageEndOffset;
            return false;
        }

        return CheckPages(document, ranges, cancellationToken, out reason, out offset);
    }

    /// <summary>Checks /T at either a classic entry or an xref stream object.</summary>
    /// <param name="store">The opened file.</param>
    /// <param name="offset">The declared /T.</param>
    /// <returns>Whether /T points to the main cross-reference entries.</returns>
    private static bool HasMainXref(PdfObjectStore store, long offset)
    {
        using var entry = store.Source.Lease(offset, (int)Math.Min(store.Source.Length - offset, FreeXrefEntry.Length + MaxXrefWhitespace));
        var bytes = entry.Span;
        while (!bytes.IsEmpty && bytes[0] is (byte)' ' or (byte)'\r' or (byte)'\n')
        {
            bytes = bytes[1..];
        }

        if (bytes.StartsWith(FreeXrefEntry))
        {
            return true;
        }

        return StoreParsing.TryParseObjectAt(store, offset, out _, out var value)
            && value.AsStream()?.Dictionary.IsName(KnownName.Type, KnownName.XRef) == true;
    }

    /// <summary>Checks each page dictionary and its declared uncompressed object group.</summary>
    /// <param name="document">The opened document.</param>
    /// <param name="ranges">The decoded page groups.</param>
    /// <param name="cancellationToken">Cancels the page walk.</param>
    /// <param name="reason">Describes the first fault.</param>
    /// <param name="offset">Locates the first fault.</param>
    /// <returns>Whether each declared group contains its page and objects.</returns>
    private static bool CheckPages(PdfDocument document, PdfHintPageRange[] ranges, CancellationToken cancellationToken, out string reason, out long offset)
    {
        var store = document.Objects;
        for (var index = 0; index < ranges.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var range = ranges[index];
            var page = PdfDocumentPages.GetPage(document, index);
            offset = StoreRevisions.GetDefinitionOffset(store, page.Id.Number);
            if (page.Id.Number != range.FirstObjectNumber || offset != range.Offset)
            {
                reason = "A linearization page hint does not point to its page dictionary.";
                return false;
            }

            if (ContainsObjects(store, range, cancellationToken))
            {
                continue;
            }

            reason = "A linearization page hint includes an object outside its byte range.";
            return false;
        }

        reason = string.Empty;
        offset = 0;
        return true;
    }

    /// <summary>Checks the objects counted in a page group that have their own file offsets.</summary>
    /// <param name="store">The opened file.</param>
    /// <param name="range">The page group.</param>
    /// <param name="cancellationToken">Cancels the object walk.</param>
    /// <returns>Whether each uncompressed object begins inside the group.</returns>
    private static bool ContainsObjects(PdfObjectStore store, PdfHintPageRange range, CancellationToken cancellationToken)
    {
        var end = range.Offset + range.Length;
        for (var number = range.FirstObjectNumber; number < range.FirstObjectNumber + range.ObjectCount; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (store.XrefState.GetType(number) != XrefEntryType.InFile)
            {
                continue;
            }

            var offset = StoreRevisions.GetDefinitionOffset(store, number);
            if (offset < range.Offset || offset >= end)
            {
                return false;
            }
        }

        return true;
    }
}
