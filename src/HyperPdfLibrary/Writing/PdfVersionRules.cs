// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// Works out the version a written file declares (ISO 32000-2 §7.5.2, §7.2.2). The version is never lowered: it is the
/// newest of the header, the catalog's <c>/Version</c>, the caller's minimum and what the features in use need.
/// </summary>
internal static class PdfVersionRules
{
    /// <summary>Gets the version in the file header, or the unset version when it is missing or not a version.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The version.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfVersion Header(PdfObjectStore store) => PdfVersion.ParseOrUnset(store.Version);

    /// <summary>Gets the version the catalog's <c>/Version</c> names, or the unset version when there is none.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The version.</returns>
    internal static PdfVersion Catalog(PdfObjectStore store) =>
        store.Catalog.GetName(KnownName.Version) is { IsNone: false } name ? PdfVersion.ParseOrUnset(store.Names.GetString(name)) : default;

    /// <summary>Gets the version a feature of the document needs.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="objectStreams">Whether the file is written with object streams and a cross-reference stream (PDF 1.5).</param>
    /// <returns>The version, or the unset version when the document needs nothing newer than 1.0.</returns>
    internal static PdfVersion Needed(PdfObjectStore store, bool objectStreams)
    {
        var needed = objectStreams ? PdfVersion.Pdf15 : default;

        // AES-GCM (ISO/TS 32003) and the PDF MAC (ISO/TS 32004) both extend PDF 2.0.
        return AesGcmExtension.IsRequired(store) || PdfMacExtension.IsRequired(store) ? PdfVersion.Max(needed, PdfVersion.Pdf20) : needed;
    }

    /// <summary>Gets the version a whole-file save writes in the header.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="minimum">The oldest version the caller allows, or the unset version.</param>
    /// <param name="objectStreams">Whether the file is written with object streams and a cross-reference stream.</param>
    /// <returns>The version; PDF 1.4 when nothing says otherwise.</returns>
    internal static PdfVersion Resolve(PdfObjectStore store, PdfVersion minimum, bool objectStreams)
    {
        var version = PdfVersion.Max(PdfVersion.Max(Header(store), Catalog(store)), PdfVersion.Max(minimum, Needed(store, objectStreams)));
        return version.IsUnset ? PdfVersion.Pdf14 : version;
    }
}
