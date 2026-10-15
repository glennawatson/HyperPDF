// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.PdfUa;

/// <summary>
/// Repairs the catalog entries PDF/UA-1 requires: <c>/MarkInfo /Marked true</c> and <c>/ViewerPreferences /DisplayDocTitle true</c>
/// (ISO 14289-1:2014, 7.1) and the document language <c>/Lang</c> (7.2). A language is set only from one the file already
/// states: the XMP <c>dc:language</c> or the <c>/Lang</c> of a structure element. The repair never invents one.
/// </summary>
internal static class PdfUaCatalogRepair
{
    /// <summary>The local name of the Dublin Core language property.</summary>
    private const string LanguageProperty = "language";

    /// <summary>The most structure elements searched for a language.</summary>
    private const int MaxElementsSearched = 4096;

    /// <summary>Fixes the catalog entries that the preflight found wrong.</summary>
    /// <param name="document">The document, read after any earlier repair.</param>
    /// <param name="transaction">The edit.</param>
    /// <param name="wanted">The requirements the preflight found broken.</param>
    internal static void Repair(PdfDocument document, PdfEditTransaction transaction, HashSet<PdfUaRequirement> wanted)
    {
        var store = document.Objects;
        var rootRaw = store.Trailer.GetRaw(KnownName.Root);
        if (!rootRaw.IsReference || StoreReading.GetDictionary(store, rootRaw.AsReference()) is not { } current)
        {
            return;
        }

        var catalog = current.Clone();
        var changed = false;
        if (wanted.Contains(PdfUaRequirement.Marked) && catalog.ContainsKey(KnownName.StructTreeRoot))
        {
            changed |= SetFlag(transaction, store, catalog, KnownName.MarkInfo, store.Names.Intern("Marked"u8));
        }

        if (wanted.Contains(PdfUaRequirement.DisplayDocTitle))
        {
            changed |= SetFlag(transaction, store, catalog, KnownName.ViewerPreferences, store.Names.Intern("DisplayDocTitle"u8));
        }

        if (wanted.Contains(PdfUaRequirement.Language) && FindLanguage(document) is { } language)
        {
            catalog.Set(KnownName.Lang, PdfValue.FromString(PdfText.Encode(language)));
            changed = true;
        }

        if (changed)
        {
            transaction.Replace(rootRaw.AsReference(), PdfValue.FromDictionary(catalog));
        }
    }

    /// <summary>Sets a boolean entry to true in a dictionary the catalog holds, creating the dictionary when it is missing.</summary>
    /// <param name="transaction">The edit.</param>
    /// <param name="store">The document's objects.</param>
    /// <param name="catalog">The catalog copy being changed.</param>
    /// <param name="key">The catalog key of the dictionary.</param>
    /// <param name="flag">The entry to set.</param>
    /// <returns><see langword="true"/> when the catalog copy itself changed; an indirect dictionary is replaced in its own object.</returns>
    private static bool SetFlag(PdfEditTransaction transaction, PdfObjectStore store, PdfDictionary catalog, KnownName key, PdfName flag)
    {
        var raw = catalog.GetRaw(key);
        var copy = StoreReading.Resolve(store, raw).AsDictionary()?.Clone() ?? new PdfDictionary(store, 1);
        copy.Set(flag, PdfValue.FromBoolean(true));
        if (raw.IsReference)
        {
            transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(copy));
            return false;
        }

        catalog.Set(key, PdfValue.FromDictionary(copy));
        return true;
    }

    /// <summary>Finds a language the file already states.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The language, or <see langword="null"/> when the file names none.</returns>
    private static string? FindLanguage(PdfDocument document)
    {
        if (PdfDocumentMetadata.GetXmp(document)?.GetValues(XmpMetadata.DublinCoreNamespace, LanguageProperty) is { Length: > 0 } declared
            && !string.IsNullOrWhiteSpace(declared[0]))
        {
            return declared[0].Trim();
        }

        var tree = PdfDocumentTagged.GetStructureTree(document);
        var budget = MaxElementsSearched;
        if (tree is not null)
        {
            foreach (var root in tree.Roots)
            {
                if (FindElementLanguage(root, ref budget) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    /// <summary>Searches an element and its descendants for the first <c>/Lang</c>.</summary>
    /// <param name="element">The element.</param>
    /// <param name="budget">How many more elements may be searched.</param>
    /// <returns>The language, or <see langword="null"/>.</returns>
    private static string? FindElementLanguage(PdfStructureElement element, ref int budget)
    {
        if (budget <= 0)
        {
            return null;
        }

        budget--;
        if (element.Dictionary.GetText(KnownName.Lang) is { Length: > 0 } own)
        {
            return own;
        }

        foreach (var kid in element.Kids)
        {
            if (kid.Kind == PdfStructureKidKind.Element && kid.Element is { } child && FindElementLanguage(child, ref budget) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
