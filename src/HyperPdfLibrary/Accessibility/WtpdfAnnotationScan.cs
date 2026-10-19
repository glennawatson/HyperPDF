// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks the annotation, form and language rules of Well-Tagged PDF 1.0 clauses 8.4.4, 8.9 and 8.10.1.</summary>
internal static class WtpdfAnnotationScan
{
    /// <summary>Checks the catalog and every page's annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    internal static void Run(PdfDocument document, List<PdfWtpdfFinding> findings, CancellationToken cancellationToken)
    {
        CheckCatalog(document.Catalog, findings);
        for (var index = 0; index < document.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckPage(document, PdfDocumentPages.GetPage(document, index).Dictionary, findings);
        }
    }

    /// <summary>Requires a catalog language (clause 8.4.4) and no XFA form (clause 8.10.1).</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckCatalog(PdfDictionary catalog, List<PdfWtpdfFinding> findings)
    {
        if (catalog.GetStringBytes(KnownName.Lang).IsEmpty)
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.4.4", "The document catalog needs a Lang entry with a language.", 0));
        }

        if (catalog.GetDictionary(KnownName.AcroForm) is { } form && !form.Get(KnownName.XFA).IsNull)
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.10.1", "An XFA form cannot be present.", 0));
        }
    }

    /// <summary>Checks one page's annotations.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page dictionary.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckPage(PdfDocument document, PdfDictionary page, List<PdfWtpdfFinding> findings)
    {
        if (page.GetArray(KnownName.Annots) is not { Count: > 0 } annotations)
        {
            return;
        }

        if (page.GetName(KnownName.Tabs).ToKnownName() is not (KnownName.S or KnownName.A or KnownName.W))
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.9.3.3", "A page with annotations needs a Tabs entry of S, A or W.", 0));
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is { } annotation)
            {
                CheckAnnotation(document.Objects.Names, annotation, findings);
            }
        }
    }

    /// <summary>Requires a markup annotation's <c>RC</c> and <c>Contents</c> to say the same, when both are strings.</summary>
    /// <param name="subtype">The annotation's subtype.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="findings">Receives the finding.</param>
    private static void CheckRichText(KnownName subtype, PdfDictionary annotation, List<PdfWtpdfFinding> findings)
    {
        var rich = annotation.GetStringBytes(KnownName.RC);
        var plain = annotation.GetStringBytes(KnownName.Contents);
        if (rich.IsEmpty || plain.IsEmpty || !WtpdfAnnotationTreeScan.IsMarkup(subtype) || rich.SequenceEqual(plain))
        {
            return;
        }

        findings.Add(new(PdfWtpdfProfile.Reuse, "8.9.2.3", "A markup annotation's RC and Contents entries must say the same thing.", 0));
    }

    /// <summary>Checks one annotation by its subtype.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckAnnotation(PdfNameTable names, PdfDictionary annotation, List<PdfWtpdfFinding> findings)
    {
        var subtype = annotation.GetName(KnownName.Subtype);
        var hasContents = !annotation.GetStringBytes(KnownName.Contents).IsEmpty;
        CheckRichText(subtype.ToKnownName(), annotation, findings);
        switch (subtype.ToKnownName())
        {
            case KnownName.Sound or KnownName.Movie or KnownName.TrapNet:
            {
                findings.Add(new(PdfWtpdfProfile.Reuse, "8.9.2.4", "Sound, movie and trap network annotations are deprecated in PDF 2.0 and cannot be present.", 0));
                break;
            }

            case KnownName.Ink when !hasContents:
            {
                findings.Add(new(PdfWtpdfProfile.Accessibility, "8.9.2.4.8", "An ink annotation needs a Contents entry that describes it.", 0));
                break;
            }

            case KnownName.Widget:
            {
                WtpdfFieldScan.Check(names, annotation, findings);
                break;
            }

            case KnownName.Stamp when !hasContents && annotation.Get(KnownName.Name).IsNull:
            {
                findings.Add(new(PdfWtpdfProfile.Reuse, "8.9.2.4.7", "A stamp annotation needs a Name or a Contents entry that says what the stamp means.", 0));
                break;
            }

            case KnownName.Screen when !hasContents:
            {
                findings.Add(new(PdfWtpdfProfile.Reuse, "8.9.2.4.12", "A screen annotation needs a Contents entry.", 0));
                break;
            }

            case KnownName.RichMedia when !hasContents:
            {
                findings.Add(new(PdfWtpdfProfile.Accessibility, "8.9.2.4.19", "A RichMedia annotation needs a Contents entry that describes it.", 0));
                break;
            }

            default:
            {
                CheckOther(names, subtype, annotation, hasContents, findings);
                break;
            }
        }
    }

    /// <summary>Checks the subtypes that have no known name.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="subtype">The annotation's subtype.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="hasContents">Whether it has a Contents entry.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckOther(PdfNameTable names, PdfName subtype, PdfDictionary annotation, bool hasContents, List<PdfWtpdfFinding> findings)
    {
        if (!hasContents && subtype == names.Intern("3D"u8))
        {
            findings.Add(new(PdfWtpdfProfile.Accessibility, "8.9.2.4.19", "A 3D annotation needs a Contents entry that describes it.", 0));
        }

        if (subtype == names.Intern("FileAttachment"u8)
            && annotation.GetDictionary(names.Intern("FS"u8)) is { } specification
            && specification.Get(names.Intern("AFRelationship"u8)).IsNull)
        {
            findings.Add(new(PdfWtpdfProfile.Reuse, "8.9.2.4.10", "The file specification of a file attachment needs an AFRelationship entry.", 0));
        }
    }
}
