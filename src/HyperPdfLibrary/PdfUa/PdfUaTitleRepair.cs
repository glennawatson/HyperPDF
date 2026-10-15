// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PdfUa;

/// <summary>
/// Repairs a missing <c>dc:title</c> in the XMP metadata (ISO 14289-1:2014, 7.1) by copying the title the document
/// information dictionary already holds. A file with no title in either place is left alone, as the repair never makes one up.
/// </summary>
internal static class PdfUaTitleRepair
{
    /// <summary>Copies the document information title into the XMP packet.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The edit.</param>
    internal static void Repair(PdfDocument document, PdfEditTransaction transaction)
    {
        var store = document.Objects;
        var info = StoreReading.Resolve(store, store.Trailer.GetRaw(KnownName.Info)).AsDictionary();
        var raw = store.Catalog.GetRaw(KnownName.Metadata);
        if (info?.GetText(KnownName.Title) is not { } title || string.IsNullOrWhiteSpace(title)
            || !raw.IsReference || StoreReading.Resolve(store, raw).AsStream() is not { } stream)
        {
            return;
        }

        if (!XmpEditor.TryApply(stream.DecodeToArray(), [new XmpChange(XmpProperty.Title, title)], out var packet) || packet is null)
        {
            return;
        }

        // The packet is written uncompressed, as XMP is meant to be readable by tools that do not parse PDF.
        var dictionary = stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        _ = dictionary.Remove(KnownName.DL);
        transaction.Replace(raw.AsReference(), PdfValue.FromStream(new(dictionary, packet)));
    }
}
