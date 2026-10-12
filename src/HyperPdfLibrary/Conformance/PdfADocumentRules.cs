// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>PDF/A rules on the trailer, the catalog and the document information dictionary.</summary>
internal static class PdfADocumentRules
{
    /// <summary>The number of keys PDF/A-4 allows in /Info: /ModDate alone.</summary>
    private const int Pdf4InfoKeys = 1;

    /// <summary>Gets the rules this area checks.</summary>
    internal static PdfARule[] Rules =>
    [
        PdfARule.Encryption,
        PdfARule.CatalogMetadata,
        PdfARule.MetadataFilterPdfA1,
        PdfARule.VersionPdf20,
        PdfARule.DocumentAdditionalActions,
        PdfARule.Requirements,
        PdfARule.NeedsRendering,
        PdfARule.InfoNeedsPieceInfo,
        PdfARule.InfoOnlyModDate,
        PdfARule.PageAdditionalActions,
        PdfARule.PagePresentationSteps,
        PdfARule.XfaForm,
        PdfARule.NeedAppearances,
    ];

    /// <summary>Records the failures of this area's rules.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        var store = context.Document.Objects;
        var catalog = context.Document.Catalog;
        var names = store.Names;
        context.Require(store.Trailer.GetRaw(KnownName.Encrypt).IsNull, PdfARule.Encryption, PdfAContexts.Trailer);
        var metadata = catalog.GetStream(KnownName.Metadata);
        context.Require(metadata is not null, PdfARule.CatalogMetadata, PdfAContexts.Catalog);
        context.Require(metadata is null || metadata.Dictionary.GetRaw(KnownName.Filter).IsNull, PdfARule.MetadataFilterPdfA1, PdfAContexts.CatalogMetadata);
        context.Require(IsPdf20Version(catalog.GetName(KnownName.Version), names), PdfARule.VersionPdf20, PdfAContexts.Catalog);
        context.Require(catalog.GetRaw(names.Intern("AA"u8)).IsNull, PdfARule.DocumentAdditionalActions, PdfAContexts.Catalog);
        context.Require(catalog.GetRaw(names.Intern("Requirements"u8)).IsNull, PdfARule.Requirements, PdfAContexts.Catalog);
        context.Require(!catalog.Get(names.Intern("NeedsRendering"u8)).AsBoolean(), PdfARule.NeedsRendering, PdfAContexts.Catalog);
        CheckInfo(context, catalog, store);
        CheckForm(context, catalog, names);
        CheckPages(context, names);
    }

    /// <summary>Checks the PDF/A-4 rules on /Info: it needs /PieceInfo in the catalog and may hold only /ModDate.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="store">The objects.</param>
    private static void CheckInfo(PdfAValidationContext context, PdfDictionary catalog, PdfObjectStore store)
    {
        var raw = store.Trailer.GetRaw(KnownName.Info);
        if (raw.IsNull)
        {
            return;
        }

        context.Require(catalog.ContainsKey(store.Names.Intern("PieceInfo"u8)), PdfARule.InfoNeedsPieceInfo, PdfAContexts.Trailer);
        if (StoreReading.Resolve(store, raw).AsDictionary() is { } info)
        {
            context.Require(info.Count == Pdf4InfoKeys && info.ContainsKey(KnownName.ModDate), PdfARule.InfoOnlyModDate, PdfAContexts.Info);
        }
    }

    /// <summary>Checks the interactive form: no XFA, and appearances that need no regeneration.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="catalog">The catalog.</param>
    /// <param name="names">The name table.</param>
    private static void CheckForm(PdfAValidationContext context, PdfDictionary catalog, PdfNameTable names)
    {
        if (catalog.GetDictionary(KnownName.AcroForm) is not { } form)
        {
            return;
        }

        context.Require(form.GetRaw(names.Intern("XFA"u8)).IsNull, PdfARule.XfaForm, PdfAContexts.Form);
        context.Require(!form.Get(names.Intern("NeedAppearances"u8)).AsBoolean(), PdfARule.NeedAppearances, PdfAContexts.Form);
    }

    /// <summary>Checks each page for additional actions and presentation steps.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="names">The name table.</param>
    private static void CheckPages(PdfAValidationContext context, PdfNameTable names)
    {
        var document = context.Document;
        var actions = names.Intern("AA"u8);
        var steps = names.Intern("PresSteps"u8);
        for (var index = 0; index < document.PageCount; index++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var page = PdfDocumentPages.GetPage(document, index).Dictionary;
            var where = PdfAContexts.Page(index);
            context.Require(page.GetRaw(actions).IsNull, PdfARule.PageAdditionalActions, where);
            context.Require(page.GetRaw(steps).IsNull, PdfARule.PagePresentationSteps, where);
        }
    }

    /// <summary>Determines whether a catalog /Version is absent or a PDF 2.x version.</summary>
    /// <param name="version">The /Version name.</param>
    /// <param name="names">The name table.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsPdf20Version(PdfName version, PdfNameTable names)
    {
        if (version.IsNone)
        {
            return true;
        }

        var text = names.GetString(version);
        return text.Length == 3 && text[0] == '2' && text[1] == '.' && char.IsAsciiDigit(text[2]);
    }
}
