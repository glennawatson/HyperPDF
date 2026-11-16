// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads and sets how the pages of a document are numbered for a reader: the ranges of page labels such as <c>i, ii, iii</c> and <c>A-1, A-2</c>.</summary>
public static class PdfDocumentPageLabels
{
    /// <summary>Gets the ranges of page labels of a document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The ranges in page order; empty when the document has no page labels, in which case pages are numbered 1, 2, 3.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static PdfPageLabelRange[] GetRanges(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Catalog.GetDictionary(KnownName.PageLabels) is null)
        {
            return [];
        }

        var found = PdfDocumentLabels.GetLabelRanges(document);
        var result = new PdfPageLabelRange[found.Length];
        for (var i = 0; i < result.Length; i++)
        {
            var label = found[i].Label;
            var prefix = label.Get(KnownName.P);
            result[i] = new(
                found[i].Start,
                ReadStyle(document, label),
                prefix.Kind == PdfKind.String ? PdfText.Decode(prefix.AsStringBytes()) : string.Empty,
                label.GetInt32(KnownName.St, 1));
        }

        return result;
    }

    /// <summary>Replaces the ranges of page labels of a document. The change is one undo step.</summary>
    /// <param name="document">The document.</param>
    /// <param name="ranges">The ranges in page order; the first starts on the first page.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">There is no range, the first does not start on the first page, or the starts are not in rising order.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A range starts on a page that does not exist or its first number is below 1.</exception>
    public static void SetRanges(PdfDocument document, ReadOnlySpan<PdfPageLabelRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(document);
        Validate(document, ranges);
        var labels = Expand(document, ranges);
        PdfDocumentEditing.RunEdit(
            document,
            "Set page labels",
            PdfChangeKinds.PageChanges,
            new LabelEdit(document.Objects, labels),
            static (transaction, edit) => PdfPageLabelWriter.Write(transaction, edit.Store, edit.Labels));
    }

    /// <summary>Reads the numbering style of a range.</summary>
    /// <param name="document">The document.</param>
    /// <param name="label">The range's dictionary.</param>
    /// <returns>The style.</returns>
    private static PdfPageLabelStyle ReadStyle(PdfDocument document, PdfDictionary label)
    {
        var spelling = document.Objects.Names.GetSpelling(label.GetName(KnownName.S));
        return spelling.Length == 1 ? StyleOf(spelling[0]) : PdfPageLabelStyle.None;
    }

    /// <summary>Maps the letter the file uses for a style.</summary>
    /// <param name="letter">The letter.</param>
    /// <returns>The style.</returns>
    private static PdfPageLabelStyle StyleOf(byte letter) => letter switch
    {
        (byte)'D' => PdfPageLabelStyle.Decimal,
        (byte)'R' => PdfPageLabelStyle.UpperRoman,
        (byte)'r' => PdfPageLabelStyle.LowerRoman,
        (byte)'A' => PdfPageLabelStyle.UpperLetters,
        (byte)'a' => PdfPageLabelStyle.LowerLetters,
        _ => PdfPageLabelStyle.None,
    };

    /// <summary>Maps a style to the letter the file uses.</summary>
    /// <param name="style">The style.</param>
    /// <returns>The letter, or an empty string for none.</returns>
    private static string LetterOf(PdfPageLabelStyle style) => style switch
    {
        PdfPageLabelStyle.Decimal => "D",
        PdfPageLabelStyle.UpperRoman => "R",
        PdfPageLabelStyle.LowerRoman => "r",
        PdfPageLabelStyle.UpperLetters => "A",
        PdfPageLabelStyle.LowerLetters => "a",
        _ => string.Empty,
    };

    /// <summary>Checks that the ranges can be written.</summary>
    /// <param name="document">The document.</param>
    /// <param name="ranges">The ranges.</param>
    /// <exception cref="ArgumentException">The ranges are empty, do not start on the first page or are not in rising order.</exception>
    private static void Validate(PdfDocument document, ReadOnlySpan<PdfPageLabelRange> ranges)
    {
        if (ranges.IsEmpty || ranges[0].StartPage != 0)
        {
            throw new ArgumentException("The first range must start on the first page.", nameof(ranges));
        }

        for (var i = 0; i < ranges.Length; i++)
        {
            ArgumentNullException.ThrowIfNull(ranges[i]);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)ranges[i].StartPage, (uint)document.PageCount, nameof(ranges));
            ArgumentOutOfRangeException.ThrowIfLessThan(ranges[i].FirstNumber, 1, nameof(ranges));
            if (i > 0 && ranges[i].StartPage <= ranges[i - 1].StartPage)
            {
                throw new ArgumentException("The ranges must start on rising pages.", nameof(ranges));
            }
        }
    }

    /// <summary>Gives every page its range's style and its number.</summary>
    /// <param name="document">The document.</param>
    /// <param name="ranges">The ranges, which were validated.</param>
    /// <returns>One label for each page.</returns>
    private static PdfPageLabel[] Expand(PdfDocument document, ReadOnlySpan<PdfPageLabelRange> ranges)
    {
        var labels = new PdfPageLabel[document.PageCount];
        for (var r = 0; r < ranges.Length; r++)
        {
            var range = ranges[r];
            var style = new PdfDictionary(document.Objects);
            if (LetterOf(range.Style) is { Length: > 0 } letter)
            {
                style.Set(KnownName.S, PdfValue.FromName(document.Objects.Names.Intern(letter)));
            }

            if (range.Prefix.Length > 0)
            {
                PdfAnnotations.SetText(style, KnownName.P, range.Prefix);
            }

            var end = r + 1 < ranges.Length ? ranges[r + 1].StartPage : labels.Length;
            for (var page = range.StartPage; page < end; page++)
            {
                labels[page] = new(style, range.FirstNumber + (page - range.StartPage));
            }
        }

        return labels;
    }

    /// <summary>The labels an edit writes.</summary>
    /// <param name="Store">The document's objects.</param>
    /// <param name="Labels">One label for each page.</param>
    private sealed record LabelEdit(PdfObjectStore Store, PdfPageLabel[] Labels);
}
