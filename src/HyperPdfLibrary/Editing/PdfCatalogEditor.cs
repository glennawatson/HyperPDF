// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Writes the catalog entries that decide how a viewer opens a document: <c>/ViewerPreferences</c> (ISO 32000-2 12.2),
/// <c>/PageLayout</c> and <c>/PageMode</c> (7.7.2), <c>/OpenAction</c> and the catalog's <c>/AA</c> (12.6.3).
/// </summary>
internal static class PdfCatalogEditor
{
    /// <summary>The most copies the standard lets a document ask for.</summary>
    private const int MaxCopies = 5;

    /// <summary>The numbers in a page range: the first page and the last.</summary>
    private const int PairSize = 2;

    /// <summary>The first version with two-column and two-page layouts (7.7.2).</summary>
    private static readonly PdfVersion TwoPageVersion = new(1, 5);

    /// <summary>The first version with the attachments panel page mode (7.7.2).</summary>
    private static readonly PdfVersion AttachmentsVersion = new(1, 6);

    /// <summary>The first version with the print scaling, duplex, tray and copies preferences (Table 147).</summary>
    private static readonly PdfVersion PrintPreferencesVersion = new(1, 7);

    /// <summary>The page modes a document may return to from full screen (Table 147).</summary>
    private static readonly string[] NonFullScreenModes = ["UseNone", "UseOutlines", "UseThumbs", "UseOC"];

    /// <summary>The page boundaries (7.7.3.3).</summary>
    private static readonly string[] Boundaries = ["MediaBox", "CropBox", "BleedBox", "TrimBox", "ArtBox"];

    /// <summary>The duplex modes (Table 147).</summary>
    private static readonly string[] DuplexModes = ["Simplex", "DuplexFlipShortEdge", "DuplexFlipLongEdge"];

    /// <summary>Changes the viewer preferences.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="change">The changes.</param>
    /// <exception cref="ArgumentException">A value is not one the standard allows.</exception>
    /// <exception cref="InvalidOperationException">The change would break a PDF/UA claim.</exception>
    internal static void SetViewerPreferences(PdfObjectEdits edits, PdfDocument document, PdfViewerPreferencesEdit change)
    {
        Validate(change);
        if (change.DisplayDocTitle == false)
        {
            PdfEditConformance.RequireDocumentTitleShown(document);
        }

        var catalog = edits.Catalog();
        var preferences = PdfEditDictionaries.Child(edits, catalog, KnownName.ViewerPreferences);
        SetFlag(preferences, "HideToolbar"u8, change.HideToolbar);
        SetFlag(preferences, "HideMenubar"u8, change.HideMenubar);
        SetFlag(preferences, "HideWindowUI"u8, change.HideWindowUI);
        SetFlag(preferences, "FitWindow"u8, change.FitWindow);
        SetFlag(preferences, "CenterWindow"u8, change.CenterWindow);
        SetFlag(preferences, "DisplayDocTitle"u8, change.DisplayDocTitle);
        SetFlag(preferences, "PickTrayByPDFSize"u8, change.PickTrayByPdfSize);
        SetName(preferences, "NonFullScreenPageMode"u8, change.NonFullScreenPageMode);
        SetName(preferences, "Direction"u8, change.Direction);
        SetName(preferences, "ViewArea"u8, change.ViewArea);
        SetName(preferences, "ViewClip"u8, change.ViewClip);
        SetName(preferences, "PrintArea"u8, change.PrintArea);
        SetName(preferences, "PrintClip"u8, change.PrintClip);
        SetName(preferences, "PrintScaling"u8, change.PrintScaling);
        SetName(preferences, "Duplex"u8, change.Duplex);
        if (change.NumCopies is { } copies)
        {
            preferences.Set(preferences.Owner!.Names.Intern("NumCopies"u8), PdfValue.FromInteger(copies));
        }

        if (change.PrintPageRange is { } ranges)
        {
            SetPrintPageRange(preferences, ranges);
        }

        if (preferences.Count == 0)
        {
            _ = catalog.Remove(KnownName.ViewerPreferences);
        }
        else if (HasPrintPreference(change))
        {
            RaiseVersion(edits, PrintPreferencesVersion);
        }
    }

    /// <summary>Sets the page layout.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="layout">The layout.</param>
    /// <exception cref="ArgumentOutOfRangeException">The layout is not defined.</exception>
    internal static void SetPageLayout(PdfObjectEdits edits, PdfPageLayout layout)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)layout, (int)PdfPageLayout.TwoPageRight);
        ArgumentOutOfRangeException.ThrowIfLessThan((int)layout, (int)PdfPageLayout.SinglePage);
        var catalog = edits.Catalog();
        if (layout == PdfPageLayout.SinglePage)
        {
            _ = catalog.Remove(catalog.Owner!.Names.Intern("PageLayout"u8));
            return;
        }

        if (layout >= PdfPageLayout.TwoColumnLeft)
        {
            RaiseVersion(edits, TwoPageVersion);
        }

        catalog.Set(catalog.Owner!.Names.Intern("PageLayout"u8), PdfValue.FromName(catalog.Owner.Names.Intern(layout.ToString())));
    }

    /// <summary>Sets the page mode.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="mode">The mode.</param>
    /// <exception cref="ArgumentOutOfRangeException">The mode is not defined.</exception>
    internal static void SetPageMode(PdfObjectEdits edits, PdfPageMode mode)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)mode, (int)PdfPageMode.UseAttachments);
        ArgumentOutOfRangeException.ThrowIfLessThan((int)mode, (int)PdfPageMode.UseNone);
        var catalog = edits.Catalog();
        if (mode == PdfPageMode.UseNone)
        {
            _ = catalog.Remove(KnownName.PageMode);
            return;
        }

        if (mode == PdfPageMode.UseAttachments)
        {
            RaiseVersion(edits, AttachmentsVersion);
        }

        catalog.Set(KnownName.PageMode, PdfValue.FromName(catalog.Owner!.Names.Intern(mode.ToString())));
    }

    /// <summary>Sets or removes the action run when the document opens.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="target">The destination or action, or <see langword="null"/> to remove it.</param>
    /// <exception cref="ArgumentException">The target is not valid.</exception>
    internal static void SetOpenAction(PdfObjectEdits edits, PdfDocument document, PdfBookmarkTarget? target)
    {
        var catalog = edits.Catalog();
        _ = catalog.Remove(KnownName.OpenAction);
        if (target is null || target.Kind == PdfBookmarkTargetKind.None)
        {
            return;
        }

        // The entry takes the same destination or action as a bookmark; build it on a scratch dictionary and lift the result.
        var scratch = new PdfDictionary(edits.Store);
        PdfActionTargetWriter.Apply(document, scratch, target);
        if (scratch.GetRaw(KnownName.A) is { IsNull: false } action)
        {
            catalog.Set(KnownName.OpenAction, action);
            return;
        }

        var destination = scratch.GetRaw(KnownName.Dest);

        // A name is not a destination here (Table 29): wrap it in a GoTo action.
        catalog.Set(
            KnownName.OpenAction,
            destination.AsArray() is not null ? destination : PdfValue.FromDictionary(PdfDestinationWriter.CreateGoTo(edits.Store, destination)));
    }

    /// <summary>Sets or removes the script that runs on a document event.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="document">The document.</param>
    /// <param name="documentEvent">The event.</param>
    /// <param name="script">The JavaScript, or <see langword="null"/> to remove the action.</param>
    /// <exception cref="ArgumentOutOfRangeException">The event is not defined.</exception>
    /// <exception cref="InvalidOperationException">The document claims PDF/A, which forbids scripts.</exception>
    internal static void SetDocumentAction(PdfObjectEdits edits, PdfDocument document, PdfDocumentActionEvent documentEvent, string? script)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((int)documentEvent, (int)PdfDocumentActionEvent.DidPrint);
        ArgumentOutOfRangeException.ThrowIfLessThan((int)documentEvent, (int)PdfDocumentActionEvent.WillClose);
        var catalog = edits.Catalog();
        var key = catalog.Owner!.Names.Intern(EventKey(documentEvent));
        if (string.IsNullOrEmpty(script))
        {
            if (catalog.GetDictionary(KnownName.AA) is not null)
            {
                var actions = PdfEditDictionaries.Child(edits, catalog, KnownName.AA);
                _ = actions.Remove(key);
                if (actions.Count == 0)
                {
                    _ = catalog.Remove(KnownName.AA);
                }
            }

            return;
        }

        PdfEditConformance.RequireScriptsAllowed(document, "scripts that run when the document is saved, printed or closed");
        var additional = PdfEditDictionaries.Child(edits, catalog, KnownName.AA);
        var action = new PdfDictionary(edits.Store);
        action.Set(KnownName.S, PdfValue.FromName(KnownName.JavaScript));
        action.Set(KnownName.JS, PdfValue.FromString(PdfText.Encode(script, edits.Store.Version)));
        additional.Set(key, PdfValue.FromDictionary(action));
    }

    /// <summary>Gets the name of a document event in the catalog's <c>/AA</c> (Table 197).</summary>
    /// <param name="documentEvent">The event.</param>
    /// <returns>The key as UTF-8.</returns>
    internal static ReadOnlySpan<byte> EventKey(PdfDocumentActionEvent documentEvent) => documentEvent switch
    {
        PdfDocumentActionEvent.WillClose => "WC"u8,
        PdfDocumentActionEvent.WillSave => "WS"u8,
        PdfDocumentActionEvent.DidSave => "DS"u8,
        PdfDocumentActionEvent.WillPrint => "WP"u8,
        _ => "DP"u8,
    };

    /// <summary>Raises the document's version to at least a version, in the catalog copy.</summary>
    /// <param name="edits">The edits.</param>
    /// <param name="version">The oldest version the document may declare.</param>
    internal static void RaiseVersion(PdfObjectEdits edits, PdfVersion version)
    {
        var current = PdfVersion.Max(PdfVersionRules.Header(edits.Store), PdfVersionRules.Catalog(edits.Store));
        if (version <= current)
        {
            return;
        }

        edits.Catalog().Set(KnownName.Version, PdfValue.FromName(edits.Store.Names.Intern(version.ToString())));
    }

    /// <summary>Checks every named value in a change against the values the standard allows.</summary>
    /// <param name="change">The change.</param>
    /// <exception cref="ArgumentException">A value is not allowed.</exception>
    private static void Validate(PdfViewerPreferencesEdit change)
    {
        Require(change.NonFullScreenPageMode, NonFullScreenModes, "non-full-screen page mode");
        Require(change.Direction, ["L2R", "R2L"], "reading direction");
        Require(change.ViewArea, Boundaries, "view area");
        Require(change.ViewClip, Boundaries, "view clip");
        Require(change.PrintArea, Boundaries, "print area");
        Require(change.PrintClip, Boundaries, "print clip");
        Require(change.PrintScaling, ["None", "AppDefault"], "print scaling");
        if (!string.IsNullOrEmpty(change.Duplex))
        {
            Require(change.Duplex, DuplexModes, "duplex mode");
        }

        if (change.NumCopies is < 1 or > MaxCopies)
        {
            throw new ArgumentException("The number of copies must be from 1 to 5.", nameof(change));
        }

        ValidateRanges(change.PrintPageRange);
    }

    /// <summary>Checks that a value, when given, is one of the allowed names.</summary>
    /// <param name="value">The value, or <see langword="null"/> for no change.</param>
    /// <param name="allowed">The allowed names.</param>
    /// <param name="what">What the value is, for the message.</param>
    /// <exception cref="ArgumentException">The value is not allowed.</exception>
    private static void Require(string? value, string[] allowed, string what)
    {
        if (value is not null && Array.IndexOf(allowed, value) < 0)
        {
            throw new ArgumentException($"'{value}' is not a valid {what}. Use one of: {string.Join(", ", allowed)}.", nameof(value));
        }
    }

    /// <summary>Checks the pairs of a print page range (Table 147): first and last pages, counted from 1, in order.</summary>
    /// <param name="ranges">The numbers, or <see langword="null"/> for no change.</param>
    /// <exception cref="ArgumentException">A pair is not valid.</exception>
    private static void ValidateRanges(IReadOnlyList<int>? ranges)
    {
        if (ranges is null)
        {
            return;
        }

        if ((ranges.Count & 1) != 0)
        {
            throw new ArgumentException("The print page range needs a first and a last page for each range.", nameof(ranges));
        }

        for (var i = 0; i < ranges.Count; i += PairSize)
        {
            if (ranges[i] < 1 || ranges[i + 1] < ranges[i])
            {
                throw new ArgumentException("Each print range needs pages counted from 1, with the last page not before the first.", nameof(ranges));
            }
        }
    }

    /// <summary>Writes a boolean preference when it is given.</summary>
    /// <param name="preferences">The dictionary.</param>
    /// <param name="key">The key as UTF-8.</param>
    /// <param name="value">The value, or <see langword="null"/> for no change.</param>
    private static void SetFlag(PdfDictionary preferences, ReadOnlySpan<byte> key, bool? value)
    {
        if (value is { } flag)
        {
            preferences.Set(preferences.Owner!.Names.Intern(key), PdfValue.FromBoolean(flag));
        }
    }

    /// <summary>Writes a name preference when it is given; an empty string removes it.</summary>
    /// <param name="preferences">The dictionary.</param>
    /// <param name="key">The key as UTF-8.</param>
    /// <param name="value">The name, <see langword="null"/> for no change, or an empty string to remove the entry.</param>
    private static void SetName(PdfDictionary preferences, ReadOnlySpan<byte> key, string? value)
    {
        if (value is null)
        {
            return;
        }

        var name = preferences.Owner!.Names.Intern(key);
        if (value.Length == 0)
        {
            _ = preferences.Remove(name);
            return;
        }

        preferences.Set(name, PdfValue.FromName(preferences.Owner.Names.Intern(value)));
    }

    /// <summary>Writes the print page range; no numbers remove it.</summary>
    /// <param name="preferences">The dictionary.</param>
    /// <param name="ranges">The first and last page of each range, counted from 1.</param>
    private static void SetPrintPageRange(PdfDictionary preferences, IReadOnlyList<int> ranges)
    {
        var key = preferences.Owner!.Names.Intern("PrintPageRange"u8);
        if (ranges.Count == 0)
        {
            _ = preferences.Remove(key);
            return;
        }

        var array = new PdfArray(preferences.Owner, ranges.Count);
        for (var i = 0; i < ranges.Count; i++)
        {
            array.Add(PdfValue.FromInteger(ranges[i]));
        }

        preferences.Set(key, PdfValue.FromArray(array));
    }

    /// <summary>Determines whether a change sets a preference that needs PDF 1.7.</summary>
    /// <param name="change">The change.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasPrintPreference(PdfViewerPreferencesEdit change) =>
        change.PrintArea is not null || change.PrintClip is not null || change.ViewArea is not null || change.ViewClip is not null
        || change.PrintScaling is not null || change.Duplex is not null || change.PickTrayByPdfSize is not null || change.NumCopies is not null
        || change.PrintPageRange is not null;
}
