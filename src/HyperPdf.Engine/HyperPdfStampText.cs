// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Documents;

namespace HyperPdf.Engine;

/// <summary>Writes the words stamped on a page from the stamp's text, which may hold the page's number, the number of pages, a running number and the date.</summary>
internal static class HyperPdfStampText
{
    /// <summary>The placeholder for the page's own number, which is its label when the document has labels.</summary>
    internal const string PageToken = "{page}";

    /// <summary>The placeholder for the number of pages.</summary>
    internal const string PagesToken = "{pages}";

    /// <summary>The placeholder for the running number of the stamped pages.</summary>
    internal const string NumberToken = "{number}";

    /// <summary>The placeholder for today's date.</summary>
    internal const string DateToken = "{date}";

    /// <summary>The longest run number that is written, which keeps zero padding to a sensible width.</summary>
    internal const int MostDigits = 12;

    /// <summary>The format today's date is written in.</summary>
    private const string DateFormat = "yyyy-MM-dd";

    /// <summary>Writes the words for one page.</summary>
    /// <param name="stamp">The stamp.</param>
    /// <param name="pageLabel">What the page is called: its label, or its place counted from one.</param>
    /// <param name="pageCount">The number of pages in the document.</param>
    /// <param name="order">The place of the page among the stamped pages, counted from zero.</param>
    /// <param name="today">Today's date.</param>
    /// <returns>The words.</returns>
    internal static string For(PageTextStamp stamp, string pageLabel, int pageCount, int order, DateTimeOffset today)
    {
        var text = stamp.Text;
        if (text.Contains(PageToken, StringComparison.Ordinal))
        {
            text = text.Replace(PageToken, pageLabel, StringComparison.Ordinal);
        }

        if (text.Contains(PagesToken, StringComparison.Ordinal))
        {
            text = text.Replace(PagesToken, pageCount.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        }

        if (text.Contains(NumberToken, StringComparison.Ordinal))
        {
            var number = ((long)stamp.FirstNumber + order).ToString(CultureInfo.InvariantCulture);
            var digits = Math.Clamp(stamp.Digits, 0, MostDigits);
            text = text.Replace(NumberToken, number.PadLeft(digits, '0'), StringComparison.Ordinal);
        }

        return text.Contains(DateToken, StringComparison.Ordinal)
            ? text.Replace(DateToken, today.ToString(DateFormat, CultureInfo.InvariantCulture), StringComparison.Ordinal)
            : text;
    }
}
