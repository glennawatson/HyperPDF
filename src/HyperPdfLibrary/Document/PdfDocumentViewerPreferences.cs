// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads document viewer preferences.</summary>
public static class PdfDocumentViewerPreferences
{
    /// <summary>The page mode when the catalog names none.</summary>
    private const string DefaultPageMode = "UseNone";

    /// <summary>The page boundary used when a preference names none.</summary>
    private const string DefaultBoundary = "CropBox";

    /// <summary>The transition style when a transition dictionary names none.</summary>
    private const string DefaultTransitionStyle = "R";

    /// <summary>The default direction of a transition's name <c>None</c>.</summary>
    private const int NoDirection = -1;

    /// <summary>Gets the viewer preferences with the page mode and layout. Missing entries keep the PDF defaults.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The preferences.</returns>
    public static PdfViewerPreferences GetViewerPreferences(PdfDocument document)
    {
        var prefs = document.Catalog.GetDictionary(KnownName.ViewerPreferences);
        return new()
        {
            PageLayout = document.Catalog.NameText("PageLayout") ?? "SinglePage",
            PageMode = document.Catalog.NameText("PageMode") ?? PdfDocumentViewerPreferences.DefaultPageMode,
            HideToolbar = PdfDocumentViewerPreferences.PreferenceFlag(prefs, "HideToolbar"),
            HideMenubar = PdfDocumentViewerPreferences.PreferenceFlag(prefs, "HideMenubar"),
            HideWindowUI = PdfDocumentViewerPreferences.PreferenceFlag(prefs, "HideWindowUI"),
            FitWindow = PdfDocumentViewerPreferences.PreferenceFlag(prefs, "FitWindow"),
            CenterWindow = PdfDocumentViewerPreferences.PreferenceFlag(prefs, "CenterWindow"),
            DisplayDocTitle = PdfDocumentViewerPreferences.PreferenceFlag(prefs, "DisplayDocTitle"),
            NonFullScreenPageMode = PdfDocumentViewerPreferences.PreferenceName(prefs, "NonFullScreenPageMode", PdfDocumentViewerPreferences.DefaultPageMode),
            Direction = PdfDocumentViewerPreferences.PreferenceName(prefs, "Direction", "L2R"),
            ViewArea = PdfDocumentViewerPreferences.PreferenceName(prefs, "ViewArea", PdfDocumentViewerPreferences.DefaultBoundary),
            ViewClip = PdfDocumentViewerPreferences.PreferenceName(prefs, "ViewClip", PdfDocumentViewerPreferences.DefaultBoundary),
            PrintArea = PdfDocumentViewerPreferences.PreferenceName(prefs, "PrintArea", PdfDocumentViewerPreferences.DefaultBoundary),
            PrintClip = PdfDocumentViewerPreferences.PreferenceName(prefs, "PrintClip", PdfDocumentViewerPreferences.DefaultBoundary),
            PrintScaling = PdfDocumentViewerPreferences.PreferenceName(prefs, "PrintScaling", "AppDefault"),
            Duplex = prefs?.NameText("Duplex"),
            PickTrayByPdfSize = PdfDocumentViewerPreferences.PreferenceFlag(prefs, "PickTrayByPDFSize"),
            PrintPageRange = PdfDocumentMedia.ToInt32Array(prefs?.Array("PrintPageRange")),
            NumCopies = prefs?.Int("NumCopies", 1) ?? 1,
            Enforce = prefs?.Array("Enforce").NameTexts() ?? [],
        };
    }

    /// <summary>
    /// Copies the viewer preferences dictionary (<c>/ViewerPreferences</c>, ISO 32000-2 12.2) of one document into another,
    /// replacing the target's. <c>/PrintPageRange</c> is left out, because it names pages of the source.
    /// </summary>
    /// <param name="target">The document that receives the preferences.</param>
    /// <param name="source">The document they are copied from; it must stay open until the target is saved.</param>
    /// <param name="cancellationToken">Cancels the copy; the target is left unchanged.</param>
    /// <returns><see langword="true"/> when the source has preferences and they were copied.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool CopyViewerPreferences(PdfDocument target, PdfDocument source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Catalog.GetDictionary(KnownName.ViewerPreferences) is null)
        {
            return false;
        }

        // The text pages read the reading direction from the viewer preferences.
        PdfDocumentEditing.RunEdit(
            target,
            "Copy viewer preferences",
            PdfChangeKinds.Other,
            PdfCacheScopes.PageContent,
            new PreferencesEdit(target, source, cancellationToken),
            Copy);
        return true;
    }

    /// <summary>Gets a page's transition (<c>/Trans</c>) with its automatic advance time (<c>/Dur</c>).</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The transition, or <see langword="null"/> when the page has none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static PdfPageTransition? GetTransition(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var advance = page.Dictionary.Value("Dur");
        return page.Dictionary.Dict("Trans") is { } trans ? PdfDocumentViewerPreferences.ReadTransition(trans, advance.IsNumber ? advance.AsNumber() : null) : null;
    }

    /// <summary>Reads a transition dictionary.</summary>
    /// <param name="trans">The transition dictionary.</param>
    /// <param name="advanceAfter">The page's automatic advance time in seconds, or null.</param>
    /// <returns>The transition.</returns>
    internal static PdfPageTransition ReadTransition(PdfDictionary trans, double? advanceAfter)
    {
        var direction = trans.Value("Di");
        var degrees = direction.Kind == PdfKind.Name ? PdfDocumentViewerPreferences.NoDirection : direction.AsInt32();
        return new(
trans.NameText("S") ?? PdfDocumentViewerPreferences.DefaultTransitionStyle,
trans.Num("D", 1),
trans.NameText("Dm") ?? "H",
trans.NameText("M") ?? "I",
degrees,
trans.Num("SS", 1),
trans.Flag("B", false),
advanceAfter);
    }

    /// <summary>Reads a boolean viewer preference.</summary>
    /// <param name="prefs">The preferences dictionary, or null.</param>
    /// <param name="key">The key.</param>
    /// <returns>The value; false when missing.</returns>
    private static bool PreferenceFlag(PdfDictionary? prefs, string key) => prefs?.Flag(key, false) ?? false;

    /// <summary>Reads a name viewer preference.</summary>
    /// <param name="prefs">The preferences dictionary, or null.</param>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The value when missing.</param>
    /// <returns>The name text.</returns>
    private static string PreferenceName(PdfDictionary? prefs, string key, string fallback) => prefs?.NameText(key) ?? fallback;

    /// <summary>Copies the preferences inside the transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The copy.</param>
    private static void Copy(PdfEditTransaction transaction, PreferencesEdit edit)
    {
        var (target, source, cancellationToken) = edit;
        var importer = new PdfStoreImporter(transaction, target.Objects, source.Objects, cancellationToken);
        var copied = importer.Import(source.Objects.Catalog.GetRaw(KnownName.ViewerPreferences));
        var preferences = (StoreReading.Resolve(target.Objects, copied).AsDictionary() ?? new PdfDictionary(target.Objects)).Clone();
        _ = preferences.Remove(target.Objects.Names.Intern("PrintPageRange"u8));
        var catalog = target.Objects.Catalog.Clone();
        catalog.Set(KnownName.ViewerPreferences, PdfValue.FromDictionary(preferences));
        PdfPageTreeWriter.ReplaceCatalog(transaction, target.Objects, catalog);
    }

    /// <summary>A viewer preferences copy to apply.</summary>
    /// <param name="Target">The document that receives the preferences.</param>
    /// <param name="Source">The document they come from.</param>
    /// <param name="CancellationToken">Cancels the copy.</param>
    private sealed record PreferencesEdit(PdfDocument Target, PdfDocument Source, CancellationToken CancellationToken);
}
