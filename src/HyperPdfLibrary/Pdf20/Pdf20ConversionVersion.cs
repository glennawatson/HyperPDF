// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Sets the version the copy declares and the trailer entries PDF 2.0 requires: the header reads <c>%PDF-2.0</c>
/// (ISO 32000-2 7.5.2), the catalog's /Version is removed so the header alone states it (7.7.2), and the trailer has an
/// /ID of two byte strings of at least 16 bytes each (7.5.5).
/// </summary>
internal static class Pdf20ConversionVersion
{
    /// <summary>The version a PDF 2.0 file declares.</summary>
    internal const string Version = "2.0";

    /// <summary>The fewest bytes a file identifier string may have (Table 15).</summary>
    private const int MinimumIdLength = 16;

    /// <summary>Sets the version and the file identifier.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(Pdf20ConversionSession session)
    {
        var store = session.Store;
        if (!string.Equals(store.Version, Version, StringComparison.Ordinal))
        {
            session.Changed("7.5.2", $"Changed the file header from PDF {store.Version} to PDF 2.0.");
            store.Version = Version;
        }

        if (store.Catalog.ContainsKey(KnownName.Version))
        {
            var catalog = store.Catalog.Clone();
            _ = catalog.Remove(KnownName.Version);
            PdfAConversionRewriter.ReplaceCatalog(store, catalog);
        }

        FixIdentifier(session);
    }

    /// <summary>
    /// Removes a missing or too short file identifier, so the writer makes a valid one, and tells the user. A valid one is
    /// kept, and the writer keeps its first string.
    /// </summary>
    /// <param name="session">The conversion.</param>
    private static void FixIdentifier(Pdf20ConversionSession session)
    {
        var trailer = session.Store.Trailer;
        var ids = trailer.GetArray(KnownName.ID);
        if (ids is not null && IsValid(ids))
        {
            return;
        }

        _ = trailer.Remove(KnownName.ID);
        session.Changed(
            "7.5.5",
            ids is null ? "Added the file identifier (/ID) that PDF 2.0 requires." : "Replaced the file identifier (/ID) with one of the length PDF 2.0 requires.");
    }

    /// <summary>Determines whether an identifier is two byte strings of at least 16 bytes.</summary>
    /// <param name="ids">The /ID array.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsValid(PdfArray ids)
    {
        if (ids.Count != 2)
        {
            return false;
        }

        for (var i = 0; i < ids.Count; i++)
        {
            var part = ids.Get(i);
            if (part.Kind != PdfKind.String || part.AsStringBytes().Length < MinimumIdLength)
            {
                return false;
            }
        }

        return true;
    }
}
