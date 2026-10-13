// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Extensions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// The developer extension that a document with a PDF MAC declares in its catalog's /Extensions dictionary under the ISO_
/// prefix (ISO/TS 32004:2024 clause 4, ISO 32000-2 7.12), and the repair that adds it to a saved document that lacks it.
/// The declaration is merged by <see cref="DeveloperExtensionMerge"/>, as every developer extension is.
/// </summary>
internal static class PdfMacExtension
{
    /// <summary>The ExtensionLevel of ISO/TS 32004.</summary>
    internal const int Level = 32_004;

    /// <summary>Gets the extension of the 2024 edition; the colon of the revision is part of the identifier.</summary>
    private static PdfDeveloperExtension Extension { get; } = new("ISO_", "2.0", Level, "https://www.iso.org/standard/45877.html") { Revision = ":2024" };

    /// <summary>Determines whether a saved document shall declare the extension: it needs a PDF MAC.</summary>
    /// <param name="store">The document.</param>
    /// <returns><see langword="true"/> when the permissions require a PDF MAC or an earlier revision has one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsRequired(PdfObjectStore store) => PdfMacSeal.IsNeeded(store);

    /// <summary>Determines whether the catalog declares the extension: an ISO_ entry with ExtensionLevel 32004.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="names">The name table.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDeclared(PdfDictionary catalog, PdfNameTable names) => Extension.IsDeclaredIn(catalog, names);

    /// <summary>Copies the catalog with the extension declared, keeping other extensions.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="store">The document.</param>
    /// <returns>The copy.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfDictionary Declare(PdfDictionary catalog, PdfObjectStore store) =>
        DeveloperExtensionMerge.Merge(store, catalog, Extension, default, true) ?? catalog;
}
