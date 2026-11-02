// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>The PDF/X-4 rules that read each page: page boxes and annotations.</summary>
internal static class PdfXPageRules
{
    /// <summary>Where the page geometry rules are stated.</summary>
    private const string Geometry = "ISO 15930-7; PDF/X in a Nutshell, core principles: bleed and trim";

    /// <summary>Where the prohibited annotation rules are stated.</summary>
    private const string Prohibited = "ISO 15930-7; PDF/X in a Nutshell, core principles: prohibited features";

    /// <summary>The rule name of the trim and art box check.</summary>
    private const string BoxesRule = "PageBoxes";

    /// <summary>The rule name of the bleed box check.</summary>
    private const string BleedRule = "BleedBox";

    /// <summary>The rule name of the annotation check.</summary>
    private const string AnnotationsRule = "Annotations";

    /// <summary>Checks the TrimBox/ArtBox, BleedBox and annotation rules of every page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Stops the scan between pages.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Check(PdfDocument document, List<PdfXFinding> findings, CancellationToken cancellationToken)
    {
        var boxes = new List<PdfXFinding>();
        var bleeds = new List<PdfXFinding>();
        var annotations = new List<PdfXFinding>();
        for (var i = 0; i < document.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = PdfDocumentPages.GetPage(document, i);
            CheckBoxes(page.Dictionary, i + 1, boxes);
            CheckBleed(page.Dictionary, i + 1, bleeds);
            CheckAnnotations(document, page.Dictionary, i + 1, annotations);
        }

        AddOrPass(findings, boxes, new(BoxesRule, true, "Every page has exactly one of TrimBox or ArtBox.", Geometry));
        AddOrPass(findings, bleeds, new(BleedRule, true, "Every BleedBox encloses its trim area.", Geometry));
        AddOrPass(findings, annotations, new(AnnotationsRule, true, "No page has a form field, sound, movie, 3D, rich media or file attachment annotation.", Prohibited));
    }

    /// <summary>Adds the failures, or the pass when there are none.</summary>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="failures">The failures found.</param>
    /// <param name="pass">The finding to add when there are no failures.</param>
    private static void AddOrPass(List<PdfXFinding> findings, List<PdfXFinding> failures, in PdfXFinding pass)
    {
        if (failures.Count == 0)
        {
            findings.Add(pass);
            return;
        }

        findings.AddRange(failures);
    }

    /// <summary>Checks a page has exactly one of TrimBox or ArtBox.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="number">The one-based page number.</param>
    /// <param name="failures">Receives a failure.</param>
    private static void CheckBoxes(PdfDictionary page, int number, List<PdfXFinding> failures)
    {
        var trim = page.ContainsKey(KnownName.TrimBox);
        var art = page.ContainsKey(KnownName.ArtBox);
        if (trim != art)
        {
            return;
        }

        failures.Add(new(BoxesRule, false, $"Page {number} has {(trim ? "both a TrimBox and an ArtBox" : "neither a TrimBox nor an ArtBox")}; PDF/X needs exactly one.", Geometry));
    }

    /// <summary>Checks a BleedBox, when present, encloses the trim or art box.</summary>
    /// <param name="page">The page dictionary.</param>
    /// <param name="number">The one-based page number.</param>
    /// <param name="failures">Receives a failure.</param>
    private static void CheckBleed(PdfDictionary page, int number, List<PdfXFinding> failures)
    {
        if (!page.ContainsKey(KnownName.BleedBox))
        {
            return;
        }

        if (!page.TryGetRectangle(KnownName.BleedBox, out var bleed))
        {
            failures.Add(new(BleedRule, false, $"Page {number} has a BleedBox that is not a rectangle.", Geometry));
            return;
        }

        var trimKey = page.ContainsKey(KnownName.TrimBox) ? KnownName.TrimBox : KnownName.ArtBox;
        if (page.TryGetRectangle(trimKey, out var trim) && !Encloses(bleed, trim))
        {
            failures.Add(new(BleedRule, false, $"Page {number} has a BleedBox that does not enclose its trim area.", Geometry));
        }
    }

    /// <summary>Determines whether one rectangle contains another.</summary>
    /// <param name="outer">The enclosing rectangle.</param>
    /// <param name="inner">The enclosed rectangle.</param>
    /// <returns><see langword="true"/> when every edge of <paramref name="outer"/> is at or beyond the matching edge of <paramref name="inner"/>.</returns>
    private static bool Encloses(PdfRectangle outer, PdfRectangle inner) =>
        outer.Left <= inner.Left && outer.Bottom <= inner.Bottom && outer.Right >= inner.Right && outer.Top >= inner.Top;

    /// <summary>Checks a page has no annotation that cannot print or that carries content.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page dictionary.</param>
    /// <param name="number">The one-based page number.</param>
    /// <param name="failures">Receives a failure for each prohibited annotation.</param>
    private static void CheckAnnotations(PdfDocument document, PdfDictionary page, int number, List<PdfXFinding> failures)
    {
        var annots = page.GetArray(KnownName.Annots);
        for (var i = 0; annots is not null && i < annots.Count; i++)
        {
            if (annots.GetDictionary(i) is not { } annotation || !IsProhibited(annotation.GetName(KnownName.Subtype).ToKnownName()))
            {
                continue;
            }

            var subtype = document.Objects.Names.GetString(annotation.GetName(KnownName.Subtype));
            failures.Add(new(AnnotationsRule, false, $"Page {number} has a {subtype} annotation, which PDF/X forbids.", Prohibited));
        }
    }

    /// <summary>Determines whether an annotation subtype is forbidden by PDF/X.</summary>
    /// <param name="subtype">The subtype.</param>
    /// <returns><see langword="true"/> for form fields, sound, movie, screen, 3D, rich media and file attachments.</returns>
    private static bool IsProhibited(KnownName subtype) =>
        subtype is KnownName.Widget or KnownName.Sound or KnownName.Movie or KnownName.Screen
            or KnownName.ThreeD or KnownName.RichMedia or KnownName.FileAttachment;
}
