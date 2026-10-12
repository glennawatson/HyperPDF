// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// PDF/A rules on the catalog's structure entries: tagging (ISO 19005-2, 6.7.2.2 and 6.7.3.3), optional content and
/// embedded files in PDF/A-1 (ISO 19005-1, 6.1.13 and 6.1.11), the embedded files PDF/A-4f needs (ISO 19005-4, 6.9),
/// alternate presentations (ISO 19005-2, 6.10) and the language of the catalog and the structure elements.
/// </summary>
internal static class PdfACatalogStructureRules
{
    /// <summary>The context of failures in structure elements.</summary>
    private const string ElementContext = "structure element";

    /// <summary>Records the rules the catalog breaks.</summary>
    /// <param name="context">The validation.</param>
    internal static void Check(PdfAValidationContext context)
    {
        const string where = PdfAContexts.Catalog;
        var store = context.Document.Objects;
        var catalog = context.Document.Catalog;
        var marked = catalog.GetDictionary(KnownName.MarkInfo)?.Get(store.Names.Intern("Marked"u8)) ?? default;
        context.Require(marked.Kind == PdfKind.Boolean && marked.AsBoolean(), PdfARule.MarkInfoMarked, where);
        var structure = catalog.GetDictionary(KnownName.StructTreeRoot);
        context.Require(structure is not null, PdfARule.StructureTreeRoot, where);
        context.Require(catalog.GetRaw(KnownName.OCProperties).IsNull, PdfARule.OptionalContentForbidden, where);

        var names = catalog.GetDictionary(KnownName.Names);
        var embedded = names is not null && !names.GetRaw(KnownName.EmbeddedFiles).IsNull;
        context.Require(!embedded, PdfARule.EmbeddedFilesForbidden, where);
        context.Require(embedded, PdfARule.EmbeddedFilesRequired, where);
        context.Require(names is null || names.Get(store.Names.Intern("AlternatePresentations"u8)).IsNull, PdfARule.AlternatePresentations, where);

        if (catalog.Get(KnownName.Lang) is { Kind: PdfKind.String } lang)
        {
            PdfALanguageTags.Check(context, lang.AsStringBytes(), where);
        }

        if (structure is null)
        {
            return;
        }

        foreach (var element in PdfAElementWalk.Elements(store, structure, context.CancellationToken))
        {
            if (element.Get(KnownName.Lang) is { Kind: PdfKind.String } elementLang)
            {
                PdfALanguageTags.Check(context, elementLang.AsStringBytes(), ElementContext);
            }
        }
    }
}
