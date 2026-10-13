// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Extensions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Writing;

/// <summary>
/// The developer extension that an AES-GCM document declares in its catalog's /Extensions dictionary under the ISO_ prefix
/// (ISO/TS 32003:2023 clause 4, ISO 32000-2 7.12), and the repair that adds it to a saved document that lacks it. The
/// declaration is merged by <see cref="DeveloperExtensionMerge"/>, as every developer extension is.
/// </summary>
internal static class AesGcmExtension
{
    /// <summary>The ExtensionLevel of ISO/TS 32003.</summary>
    internal const int Level = 32_003;

    /// <summary>Gets the extension of the 2023 edition; the colon of the revision is part of the identifier.</summary>
    private static PdfDeveloperExtension Extension { get; } = new("ISO_", "2.0", Level, "https://www.iso.org/standard/45876.html") { Revision = ":2023" };

    /// <summary>Determines whether a document uses AES-GCM, so it shall declare the extension.</summary>
    /// <param name="store">The document.</param>
    /// <returns><see langword="true"/> when its security handler declares an AESV4 crypt filter.</returns>
    internal static bool IsRequired(PdfObjectStore store) => store.Security is { UsesAesGcm: true };

    /// <summary>Determines whether the catalog declares the extension: an ISO_ entry with ExtensionLevel 32003.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="names">The name table.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsDeclared(PdfDictionary catalog, PdfNameTable names) => Extension.IsDeclaredIn(catalog, names);

    /// <summary>
    /// Declares the extension in the catalog of a document that is about to be written with AES-GCM, as one undoable edit,
    /// so the saved file says which extension it uses. Nothing changes when the catalog already declares it.
    /// </summary>
    /// <param name="store">The document.</param>
    internal static void EnsureDeclared(PdfObjectStore store)
    {
        var root = store.Trailer.GetRaw(KnownName.Root);
        if (IsDeclared(store.Catalog, store.Names) || !root.IsReference)
        {
            return;
        }

        var transaction = PdfGuardTransactions.JoinOrBegin(store, "Declare the AES-GCM extension", out var owned);
        try
        {
            transaction.Replace(root.AsReference(), PdfValue.FromDictionary(Declare(store.Catalog, store)));
        }
        catch
        {
            if (owned)
            {
                transaction.Rollback();
            }

            throw;
        }

        if (owned)
        {
            transaction.Commit();
        }
    }

    /// <summary>Copies the catalog with the extension declared, keeping other extensions.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="store">The document.</param>
    /// <returns>The copy.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfDictionary Declare(PdfDictionary catalog, PdfObjectStore store) =>
        DeveloperExtensionMerge.Merge(store, catalog, Extension, default, true) ?? catalog;
}
