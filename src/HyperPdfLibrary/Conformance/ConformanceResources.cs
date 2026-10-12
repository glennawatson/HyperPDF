// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Finds the resources a document's content uses, by walking page content, the forms and tiling patterns it draws, soft
/// masks and annotation appearances with the optimiser's <see cref="ContentUsageScanner"/>. The walk is bounded by the
/// draw depth limit, reads each shared form once, and treats damaged content as using nothing it did not reach.
/// </summary>
internal static class ConformanceResources
{
    /// <summary>Walks every page and collects the resources its content uses.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellationToken">Stops the walk between pages.</param>
    /// <returns>The used resources, each listed once.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static ConformanceUses Read(PdfDocument document, CancellationToken cancellationToken)
    {
        // Pages are walked one at a time, not with ScanDocument: the interactive form's default resources are not drawn.
        var scanner = new ContentUsageScanner(document);
        for (var i = 0; i < document.PageCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScanPage(scanner, PdfDocumentPages.GetPage(document, i));
        }

        var uses = new ConformanceUses([], [], [], [], [], scanner.UsesDeviceColor);
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var (resources, names) in scanner.UsedNames)
        {
            foreach (var use in names)
            {
                AddUse(document.Objects, resources, use, uses, seen);
            }
        }

        AddFonts(document.Objects, scanner.PageFonts, uses, seen);
        AddFonts(document.Objects, scanner.ExcludedFonts, uses, seen);
        foreach (var number in scanner.Images.Keys)
        {
            AddImage(StoreReading.GetObject(document.Objects, new(number, 0)), uses, seen);
        }

        return uses;
    }

    /// <summary>Walks one page; content too damaged to decode uses nothing beyond what was reached.</summary>
    /// <param name="scanner">The scanner.</param>
    /// <param name="page">The page.</param>
    private static void ScanPage(ContentUsageScanner scanner, PdfPage page)
    {
        try
        {
            scanner.ScanPage(page);
        }
        catch (Exception ex) when (ex is InvalidDataException or PdfException or FormatException or OverflowException or NotSupportedException)
        {
            // A filter the library cannot decode leaves the page's content unread.
        }
    }

    /// <summary>Resolves one used resource name and adds what it names.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="resources">The resource dictionary the name was used with.</param>
    /// <param name="use">The category and name.</param>
    /// <param name="uses">The collected resources.</param>
    /// <param name="seen">The objects already collected.</param>
    private static void AddUse(PdfObjectStore objects, PdfDictionary resources, ResourceUse use, ConformanceUses uses, HashSet<object> seen)
    {
        var value = StoreReading.Resolve(objects, resources.GetDictionary(use.Category)?.GetRaw(use.Name) ?? default);
        switch (use.Category)
        {
            case KnownName.Font:
                {
                    AddDictionary(value.AsDictionary(), uses.Fonts, seen);
                    break;
                }

            case KnownName.ExtGState:
                {
                    AddDictionary(value.AsDictionary(), uses.GraphicsStates, seen);
                    break;
                }

            case KnownName.XObject:
                {
                    AddXObject(value, uses, seen);
                    break;
                }

            case KnownName.Pattern:
                {
                    AddStream(value.AsStream(), uses.TilingPatterns, seen);
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Adds a drawn XObject as a form or an image.</summary>
    /// <param name="value">The resolved XObject.</param>
    /// <param name="uses">The collected resources.</param>
    /// <param name="seen">The objects already collected.</param>
    private static void AddXObject(PdfValue value, ConformanceUses uses, HashSet<object> seen)
    {
        if (value.AsStream() is not { } stream)
        {
            return;
        }

        if (stream.Dictionary.IsName(KnownName.Subtype, KnownName.Form))
        {
            AddStream(stream, uses.Forms, seen);
            return;
        }

        AddImage(value, uses, seen);
    }

    /// <summary>Adds an image XObject's dictionary.</summary>
    /// <param name="value">The resolved image.</param>
    /// <param name="uses">The collected resources.</param>
    /// <param name="seen">The objects already collected.</param>
    private static void AddImage(PdfValue value, ConformanceUses uses, HashSet<object> seen)
    {
        if (value.AsStream() is { } image && image.Dictionary.IsName(KnownName.Subtype, KnownName.Image))
        {
            AddDictionary(image.Dictionary, uses.Images, seen);
        }
    }

    /// <summary>Adds the fonts the scanner recorded by object number.</summary>
    /// <param name="objects">The document's objects.</param>
    /// <param name="numbers">The object numbers.</param>
    /// <param name="uses">The collected resources.</param>
    /// <param name="seen">The objects already collected.</param>
    private static void AddFonts(PdfObjectStore objects, HashSet<int> numbers, ConformanceUses uses, HashSet<object> seen)
    {
        foreach (var number in numbers)
        {
            AddDictionary(StoreReading.GetObject(objects, new(number, 0)).AsDictionary(), uses.Fonts, seen);
        }
    }

    /// <summary>Adds a dictionary once.</summary>
    /// <param name="dictionary">The dictionary, or null.</param>
    /// <param name="list">The list to add to.</param>
    /// <param name="seen">The objects already collected.</param>
    private static void AddDictionary(PdfDictionary? dictionary, List<PdfDictionary> list, HashSet<object> seen)
    {
        if (dictionary is not null && seen.Add(dictionary))
        {
            list.Add(dictionary);
        }
    }

    /// <summary>Adds a stream once.</summary>
    /// <param name="stream">The stream, or null.</param>
    /// <param name="list">The list to add to.</param>
    /// <param name="seen">The objects already collected.</param>
    private static void AddStream(PdfStream? stream, List<PdfStream> list, HashSet<object> seen)
    {
        if (stream is not null && seen.Add(stream.Dictionary))
        {
            list.Add(stream);
        }
    }
}
