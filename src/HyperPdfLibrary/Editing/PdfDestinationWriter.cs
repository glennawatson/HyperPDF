// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Builds destination arrays and the actions and text strings that outlines and links use to point at them.</summary>
internal static class PdfDestinationWriter
{
    /// <summary>Builds an explicit destination array (ISO 32000-2 12.3.2.2).</summary>
    /// <param name="document">The document the page is in.</param>
    /// <param name="destination">The destination.</param>
    /// <returns>The array, starting with a reference to the page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The page index is not a page of the document.</exception>
    /// <exception cref="ArgumentException">A rectangle fit is missing a coordinate.</exception>
    internal static PdfArray Create(PdfDocument document, PdfExplicitDestination destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(destination.PageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(destination.PageIndex, document.PageCount);
        var array = new PdfArray(document.Objects);
        array.Add(PdfValue.FromReference(PdfDocumentPages.GetPage(document, destination.PageIndex).Id));
        switch (destination.Fit)
        {
            case PdfDestinationFit.Xyz:
                {
                    Add(array, KnownName.XYZ, destination.Left, destination.Top, destination.Zoom);
                    break;
                }

            case PdfDestinationFit.Fit:
                {
                    Add(array, KnownName.Fit);
                    break;
                }

            case PdfDestinationFit.FitWidth:
                {
                    Add(array, KnownName.FitH, destination.Top);
                    break;
                }

            case PdfDestinationFit.FitHeight:
                {
                    Add(array, KnownName.FitV, destination.Left);
                    break;
                }

            case PdfDestinationFit.FitRectangle:
                {
                    AddRectangle(array, destination);
                    break;
                }

            case PdfDestinationFit.FitBounds:
                {
                    Add(array, KnownName.FitB);
                    break;
                }

            case PdfDestinationFit.FitBoundsWidth:
                {
                    Add(array, KnownName.FitBH, destination.Top);
                    break;
                }

            default:
                {
                    Add(array, KnownName.FitBV, destination.Left);
                    break;
                }
        }

        return array;
    }

    /// <summary>Builds a GoTo action to a named or explicit destination.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="destination">The destination value: an array or a string.</param>
    /// <returns>The action dictionary.</returns>
    internal static PdfDictionary CreateGoTo(PdfObjectStore store, PdfValue destination)
    {
        var action = new PdfDictionary(store);
        action.Set(KnownName.S, PdfValue.FromName(KnownName.GoTo));
        action.Set(KnownName.D, destination);
        return action;
    }

    /// <summary>Appends the name and numbers of a fit.</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="fit">The fit name.</param>
    /// <param name="numbers">The numbers that follow it; a missing one is written as null.</param>
    private static void Add(PdfArray array, KnownName fit, params ReadOnlySpan<float?> numbers)
    {
        array.Add(PdfValue.FromName(fit));
        foreach (var number in numbers)
        {
            array.Add(number is { } value ? PdfValue.FromReal(value) : default);
        }
    }

    /// <summary>Appends a rectangle fit, which needs all four coordinates.</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="destination">The destination.</param>
    /// <exception cref="ArgumentException">A coordinate is missing.</exception>
    private static void AddRectangle(PdfArray array, PdfExplicitDestination destination)
    {
        if (destination is not { Left: { } left, Bottom: { } bottom, Right: { } right, Top: { } top })
        {
            throw new ArgumentException("A rectangle destination needs a left, bottom, right and top coordinate.", nameof(destination));
        }

        Add(array, KnownName.FitR, left, bottom, right, top);
    }
}
