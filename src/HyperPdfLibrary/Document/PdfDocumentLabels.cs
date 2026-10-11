// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads page labels.</summary>
public static class PdfDocumentLabels
{
    /// <summary>The letters in the alphabet styles.</summary>
    private const int LetterCount = 26;

    /// <summary>The largest number written in Roman numerals before wrapping, as PDFium does.</summary>
    private const int MaxRoman = 1_000_000;

    /// <summary>The longest run of letters written, as PDFium limits it.</summary>
    private const int MaxLetters = 1000;

    /// <summary>Gets the values of the Roman numeral symbols, largest first.</summary>
    private static ReadOnlySpan<int> RomanValues => [0x3E8, 0x384, 0x1F4, 0x190, 0x64, 0x5A, 0x32, 0x28, 0x0A, 0x09, 0x05, 0x04, 0x01];

    /// <summary>Gets the lower-case Roman numeral symbols, matching <see cref="RomanValues"/>.</summary>
    private static string[] RomanSymbols { get; } = ["m", "cm", "d", "cd", "c", "xc", "l", "xl", "x", "ix", "v", "iv", "i"];

    /// <summary>Gets a page's label, for example "iv".</summary>
    /// <param name="document">The document.</param>
    /// <param name="index">The zero based page index.</param>
    /// <returns>The label, or <see langword="null"/> when the document defines no labels or the label is empty.</returns>
    public static string? GetPageLabel(PdfDocument document, int index)
    {
        if ((uint)index >= (uint)document.PageCount || document.Catalog.GetDictionary(KnownName.PageLabels) is null)
        {
            return null;
        }

        var ranges = PdfDocumentLabels.GetLabelRanges(document);
        var range = PdfDocumentLabels.FindRange(ranges, index);
        if (range is null)
        {
            return (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        var dictionary = range.Value.Label;
        var prefix = dictionary.GetText(KnownName.P) ?? string.Empty;
        var number = index - range.Value.Start + dictionary.GetInt32(KnownName.St, 1);
        var label = prefix + PdfDocumentLabels.FormatNumber(number, document.Objects.Names.GetSpelling(dictionary.GetName(KnownName.S)));
        return label.Length == 0 ? null : label;
    }

    /// <summary>Gets the labels of some pages as style and number, for pages copied into another document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pages">The pages.</param>
    /// <returns>The label of each page, or <see langword="null"/> when the document defines no labels.</returns>
    internal static PdfPageLabel[]? CollectSourceLabels(PdfDocument document, ReadOnlySpan<PdfPage> pages)
    {
        if (document.Catalog.GetDictionary(KnownName.PageLabels) is null)
        {
            return null;
        }

        // Pages before the first range are labelled with their decimal page number; one shared style keeps them in runs.
        var ranges = PdfDocumentLabels.GetLabelRanges(document);
        var decimalStyle = new PdfDictionary(document.Objects);
        decimalStyle.Set(KnownName.S, PdfValue.FromName(KnownName.D));
        var labels = new PdfPageLabel[pages.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            var index = pages[i].Index;
            labels[i] = PdfDocumentLabels.FindRange(ranges, index) is { } range ? new(range.Label, index - range.Start + range.Label.GetInt32(KnownName.St, 1)) : new(decimalStyle, index + 1);
        }

        return labels;
    }

    /// <summary>Finds the range with the largest start at or before a page.</summary>
    /// <param name="ranges">The sorted ranges.</param>
    /// <param name="index">The page index.</param>
    /// <returns>The range, or <see langword="null"/>.</returns>
    internal static PageLabelRange? FindRange(PageLabelRange[] ranges, int index)
    {
        PageLabelRange? found = null;
        foreach (var range in ranges)
        {
            if (range.Start > index)
            {
                break;
            }

            found = range;
        }

        return found;
    }

    /// <summary>Reads the label ranges once.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The ranges, sorted by start page.</returns>
    internal static PageLabelRange[] GetLabelRanges(PdfDocument document)
    {
        var ranges = Volatile.Read(ref document.State.LabelRanges);
        if (ranges is not null)
        {
            return ranges;
        }

        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateNumbers(document.Catalog.GetDictionary(KnownName.PageLabels), entries);
        var list = new List<PageLabelRange>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Key.IsNumber && entry.Value.AsDictionary() is { } label)
            {
                list.Add(new(entry.Key.AsInt32(), label));
            }
        }

        list.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        ranges = [.. list];
        Volatile.Write(ref document.State.LabelRanges, ranges);
        return ranges;
    }

    /// <summary>Formats the number part of a label.</summary>
    /// <param name="number">The number.</param>
    /// <param name="spelling">The /S style name's spelling.</param>
    /// <returns>The text.</returns>
    private static string FormatNumber(int number, ReadOnlySpan<byte> spelling) => spelling.Length != 1 ? string.Empty : spelling[0] switch
    {
        (byte)'D' => number.ToString(CultureInfo.InvariantCulture),
        (byte)'R' => PdfDocumentLabels.Roman(number, true),
        (byte)'r' => PdfDocumentLabels.Roman(number, false),
        (byte)'A' => PdfDocumentLabels.Letters(number).ToUpperInvariant(),
        (byte)'a' => PdfDocumentLabels.Letters(number),
        _ => string.Empty,
    };

    /// <summary>Writes a number in Roman numerals.</summary>
    /// <param name="number">The number.</param>
    /// <param name="upperCase">Whether to use upper-case symbols.</param>
    /// <returns>The numerals.</returns>
    private static string Roman(int number, bool upperCase)
    {
        number %= PdfDocumentLabels.MaxRoman;
        var remaining = number;
        var length = 0;
        for (var i = 0; i < PdfDocumentLabels.RomanValues.Length && remaining > 0; i++)
        {
            var count = remaining / PdfDocumentLabels.RomanValues[i];
            length += count * PdfDocumentLabels.RomanSymbols[i].Length;
            remaining %= PdfDocumentLabels.RomanValues[i];
        }

        return string.Create(length, new RomanState(number, upperCase), static (destination, state) =>
        {
            var remaining = state.Number;
            var offset = 0;
            for (var i = 0; i < PdfDocumentLabels.RomanValues.Length && remaining > 0; i++)
            {
                var count = remaining / PdfDocumentLabels.RomanValues[i];
                remaining %= PdfDocumentLabels.RomanValues[i];
                var symbol = PdfDocumentLabels.RomanSymbols[i];
                for (var occurrence = 0; occurrence < count; occurrence++)
                {
                    foreach (var value in symbol)
                    {
                        destination[offset] = state.UpperCase ? char.ToUpperInvariant(value) : value;
                        offset++;
                    }
                }
            }
        });
    }

    /// <summary>Writes a number as letters: a..z, then aa..zz, and so on.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The letters.</returns>
    private static string Letters(int number)
    {
        if (number <= 0)
        {
            return string.Empty;
        }

        var zeroBased = number - 1;
        var count = ((zeroBased / PdfDocumentLabels.LetterCount) + 1) % PdfDocumentLabels.MaxLetters;
        return new((char)('a' + (zeroBased % PdfDocumentLabels.LetterCount)), count);
    }

    /// <summary>The value and case written into a Roman label.</summary>
    /// <param name="Number">The value after PDFium's wrap.</param>
    /// <param name="UpperCase">Whether to use upper-case symbols.</param>
    private readonly record struct RomanState(int Number, bool UpperCase);
}
