// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Sets the PDF version a converted file declares: 1.4 for PDF/A-1 (ISO 19005-1 §5), 1.7 for PDF/A-2 and PDF/A-3, which
/// are based on ISO 32000-1, and 2.0 for PDF/A-4 (ISO 19005-4:2020 §6.1.2). The catalog's /Version is removed so the
/// header alone states it.
/// </summary>
internal static class PdfAConversionVersion
{
    /// <summary>Sets the version.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(PdfAConversionSession session)
    {
        var store = session.Store;
        store.Version = Of(session.Part);
        if (!store.Catalog.ContainsKey(KnownName.Version))
        {
            return;
        }

        var catalog = store.Catalog.Clone();
        _ = catalog.Remove(KnownName.Version);
        PdfAConversionRewriter.ReplaceCatalog(store, catalog);
    }

    /// <summary>Gets the header version of a part.</summary>
    /// <param name="part">The part.</param>
    /// <returns>The version text.</returns>
    internal static string Of(int part) => part switch
    {
        PdfAClaim.Part1 => "1.4",
        PdfAClaim.Part4 => "2.0",
        _ => "1.7",
    };
}
