// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Checks the optional content and destination rules of Well-Tagged PDF 1.0: optional content configurations carry a name and no
/// automatic state (clause 8.7, accessibility level), and a link inside the document points at a structure destination (clause 8.8).
/// </summary>
internal static class WtpdfNavigationScan
{
    /// <summary>Checks the catalog's optional content and every page's links.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    internal static void Run(PdfDocument document, List<PdfWtpdfFinding> findings, CancellationToken cancellationToken)
    {
        CheckOptionalContent(document.Catalog, document.Objects.Names, findings);
        var found = false;
        for (var index = 0; !found && index < document.PageCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            found = HasPlainDestination(document.Objects.Names, PdfDocumentPages.GetPage(document, index).Dictionary);
        }

        if (!found)
        {
            return;
        }

        findings.Add(new(PdfWtpdfProfile.Reuse, "8.8", "A link that points inside the document must use a structure destination, one that names a structure element rather than a page.", 0));
    }

    /// <summary>Checks the optional content configurations (clause 8.7).</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="names">The name table.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckOptionalContent(PdfDictionary catalog, PdfNameTable names, List<PdfWtpdfFinding> findings)
    {
        if (catalog.GetDictionary(KnownName.OCProperties) is not { } properties)
        {
            return;
        }

        var configs = properties.GetArray(names.Intern("Configs"u8));
        var needName = configs is { Count: > 0 };
        if (properties.GetDictionary(KnownName.D) is { } defaults)
        {
            CheckConfiguration(defaults, needName, findings);
        }

        for (var i = 0; configs is not null && i < configs.Count; i++)
        {
            if (configs.GetDictionary(i) is { } configuration)
            {
                CheckConfiguration(configuration, needName, findings);
            }
        }
    }

    /// <summary>Checks one optional content configuration dictionary.</summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="needName">Whether the file has configurations beyond the default, so each needs a name.</param>
    /// <param name="findings">Receives the findings.</param>
    private static void CheckConfiguration(PdfDictionary configuration, bool needName, List<PdfWtpdfFinding> findings)
    {
        if (needName && string.IsNullOrEmpty(configuration.GetText(KnownName.Name)))
        {
            findings.Add(new(PdfWtpdfProfile.Accessibility, "8.7", "An optional content configuration needs a Name that is a non-empty text string.", 0));
        }

        if (!configuration.Get(KnownName.AS).IsNull)
        {
            findings.Add(new(PdfWtpdfProfile.Accessibility, "8.7", "An optional content configuration cannot have an AS entry, which changes layers by itself.", 0));
        }
    }

    /// <summary>Determines whether a page has a link that points at a page position rather than a structure element.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="page">The page dictionary.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool HasPlainDestination(PdfNameTable names, PdfDictionary page)
    {
        if (page.GetArray(KnownName.Annots) is not { } annotations)
        {
            return false;
        }

        for (var i = 0; i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is { } annotation && IsPlain(names, annotation))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an annotation's destination is an array that names a page, not a structure element.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> for a page destination. Named destinations are not followed and count as fine.</returns>
    /// <remarks>A structure destination's first entry is a structure element dictionary; a page destination's is a page dictionary (ISO 32000-2, 12.3.2.2 and 12.3.2.3).</remarks>
    private static bool IsPlain(PdfNameTable names, PdfDictionary annotation)
    {
        var destination = annotation.GetArray(KnownName.Dest);
        if (annotation.GetDictionary(KnownName.A) is { } action && action.GetName(KnownName.S).ToKnownName() == KnownName.GoTo)
        {
            destination = action.Get(names.Intern("SD"u8)).IsNull ? action.GetArray(KnownName.D) : null;
        }

        return destination is { Count: > 0 } && destination.GetDictionary(0) is { } target && target.GetName(KnownName.Type).ToKnownName() == KnownName.Page;
    }
}
