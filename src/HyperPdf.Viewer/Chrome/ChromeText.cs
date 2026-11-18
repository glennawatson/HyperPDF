// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Viewer.Text;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Prepares text for the window's controls.</summary>
internal static class ChromeText
{
    /// <summary>The UTF-16 units of a character outside the basic plane, which Backspace removes together.</summary>
    private const int SurrogatePair = 2;

    /// <summary>The mark that shows text was cut short.</summary>
    private const string Ellipsis = "…";

    /// <summary>The mark in a menu header that names its access key, which is not drawn.</summary>
    private const char AccessMark = '_';

    /// <summary>Cuts text short at its start so its end, where typing goes on, fits a width.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">How it looks.</param>
    /// <param name="width">The width it must fit.</param>
    /// <param name="runs">The text measurer.</param>
    /// <returns>The text, whole when it fits.</returns>
    internal static string FitEnd(string text, TextStyle style, int width, TextRuns runs)
    {
        var start = 0;
        while (start < text.Length && runs.Measure(text.AsSpan(start), style) > width)
        {
            start++;
        }

        if (start < text.Length && char.IsLowSurrogate(text[start]))
        {
            start++;
        }

        return start == 0 ? text : text[start..];
    }

    /// <summary>Takes the last character off text, as Backspace does. A character outside the basic plane goes as a whole.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text without its last character, or the text itself when it is empty.</returns>
    internal static string WithoutLast(string text)
    {
        if (text.Length == 0)
        {
            return text;
        }

        return text[..^(char.IsLowSurrogate(text[^1]) && text.Length > 1 ? SurrogatePair : 1)];
    }

    /// <summary>Cuts text short so it fits a width, ending it with an ellipsis.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">How it looks.</param>
    /// <param name="width">The width it must fit, in device pixels.</param>
    /// <param name="runs">The text measurer.</param>
    /// <returns>The text, whole when it fits.</returns>
    internal static string Fit(string text, TextStyle style, int width, TextRuns runs)
    {
        if (runs.Width(text, style) <= width)
        {
            return text;
        }

        // A layout is built again each time a control changes, and cuts the same names to the same widths.
        if (runs.FindFit(text, style, width) is { } kept)
        {
            return kept;
        }

        var room = width - runs.Width(Ellipsis, style);
        var low = 0;
        var high = text.Length;
        while (low < high)
        {
            var middle = (low + high + 1) >> 1;
            if (runs.Measure(text.AsSpan(0, middle), style) <= room)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }

        // A cut between the two halves of a surrogate pair would leave half a character.
        if (low > 0 && char.IsHighSurrogate(text[low - 1]))
        {
            low--;
        }

        return runs.KeepFit(text, style, width, string.Concat(text.AsSpan(0, low).TrimEnd(), Ellipsis));
    }

    /// <summary>Breaks text into lines that fit a width, at spaces where it can.</summary>
    /// <param name="text">The text.</param>
    /// <param name="style">How it looks.</param>
    /// <param name="width">The width a line must fit, in device pixels.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="lines">Receives the lines.</param>
    internal static void Wrap(string text, TextStyle style, int width, TextRuns runs, List<string> lines)
    {
        foreach (var paragraph in text.AsSpan().EnumerateLines())
        {
            var rest = paragraph;
            while (!rest.IsEmpty)
            {
                var take = LineLength(rest, style, width, runs);
                lines.Add(rest[..take].TrimEnd().ToString());
                rest = rest[take..].TrimStart();
            }
        }
    }

    /// <summary>Removes the access key mark from a menu header.</summary>
    /// <param name="header">The header, such as "_Copy".</param>
    /// <returns>The header as it is drawn.</returns>
    internal static string WithoutAccessMark(string header) =>
        header.Contains(AccessMark, StringComparison.Ordinal) ? header.Replace("_", string.Empty, StringComparison.Ordinal) : header;

    /// <summary>Finds how much of a paragraph fits on one line.</summary>
    /// <param name="text">The rest of the paragraph.</param>
    /// <param name="style">How it looks.</param>
    /// <param name="width">The width of a line.</param>
    /// <param name="runs">The text measurer.</param>
    /// <returns>The characters to put on the line, at least one.</returns>
    private static int LineLength(ReadOnlySpan<char> text, TextStyle style, int width, TextRuns runs)
    {
        if (runs.Measure(text, style) <= width)
        {
            return text.Length;
        }

        var fits = 0;
        for (var i = 1; i <= text.Length; i++)
        {
            if (i < text.Length && text[i] != ' ')
            {
                continue;
            }

            if (runs.Measure(text[..i], style) > width)
            {
                break;
            }

            fits = i;
        }

        if (fits > 0)
        {
            return fits;
        }

        // One word wider than the line is cut where the line ends.
        var cut = 1;
        while (cut < text.Length && runs.Measure(text[..(cut + 1)], style) <= width)
        {
            cut++;
        }

        return cut;
    }
}
