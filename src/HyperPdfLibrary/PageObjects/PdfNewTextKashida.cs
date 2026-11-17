// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Shaping;

namespace HyperPdfLibrary.PageObjects;

/// <summary>
/// Stretches a line of Arabic to a width the way typesetters do: the letters stay as they are and the tatweel, the stroke that joins a
/// letter to the next, is added between letters that join. Spaces cannot be widened in an embedded font, and spacing out the letters breaks the joins.
/// </summary>
internal static class PdfNewTextKashida
{
    /// <summary>The tatweel, which joins the letter before it to the letter after it.</summary>
    private const int Tatweel = 0x0640;

    /// <summary>The tatweel as a character.</summary>
    private const char TatweelChar = 'ـ';

    /// <summary>The tatweel as text.</summary>
    private const string TatweelText = "ـ";

    /// <summary>The most tatweels added at one place, so a short line is not drawn out into a long stroke.</summary>
    private const int MostAtOnePlace = 4;

    /// <summary>Adds tatweels between the joining letters of a line so it is as wide as a width, or as near as whole tatweels allow.</summary>
    /// <param name="text">The line, in logical order.</param>
    /// <param name="target">The width the line is stretched to, in user space units.</param>
    /// <param name="meter">How the line is measured; it must be set in an embedded font.</param>
    /// <returns>The line with tatweels in it; the line as it was when the font has no tatweel, the line has nowhere to put one, or there is not room for one.</returns>
    internal static string Stretch(string text, float target, in PdfNewTextMeasure.WordMeter meter)
    {
        if (meter.Source is not { } source || !source.HasGlyph(Tatweel))
        {
            return text;
        }

        var places = Walk(text, default, 0, 0);
        var tatweel = PdfNewTextMeasure.Advance(meter, TatweelText);
        var width = PdfNewTextMeasure.Advance(meter, text);
        if (places == 0 || tatweel <= 0F || target - width < tatweel)
        {
            return text;
        }

        var count = Math.Min((int)((target - width) / tatweel), places * MostAtOnePlace);
        var stretched = Insert(text, places, count);

        // Tatweels that join letters may be wider or narrower than one alone, so what was gained is measured and the count is set again when the line is too wide.
        var gained = PdfNewTextMeasure.Advance(meter, stretched) - width;
        if (gained > target - width && gained > 0F)
        {
            count = (int)(count * (target - width) / gained);
            stretched = count > 0 ? Insert(text, places, count) : text;
        }

        return stretched;
    }

    /// <summary>Adds a number of tatweels to a line, spread evenly over the places they can go.</summary>
    /// <param name="text">The line.</param>
    /// <param name="places">How many places the line has.</param>
    /// <param name="count">How many tatweels to add.</param>
    /// <returns>The line with the tatweels in it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Insert(string text, int places, int count) =>
        string.Create(text.Length + count, new Insertion(text, places, count), static (output, insertion) => Walk(insertion.Text, output, insertion.Places, insertion.Count));

    /// <summary>Goes through a line and finds the places a tatweel can go: before a letter that joins the letter before it, when that letter joins forward.</summary>
    /// <param name="text">The line.</param>
    /// <param name="output">Receives the line with the tatweels in it, or is empty to only count the places.</param>
    /// <param name="places">How many places the line has, when tatweels are added.</param>
    /// <param name="count">How many tatweels to add in all, which is spread evenly over the places.</param>
    /// <returns>The number of places.</returns>
    private static int Walk(string text, Span<char> output, int places, int count)
    {
        var found = 0;
        var written = 0;
        var joinsForward = false;
        foreach (var letter in text)
        {
            var type = ArabicJoining.TypeOf(letter);
            if (type != JoiningType.Transparent)
            {
                if (joinsForward && type is JoiningType.Dual or JoiningType.Right)
                {
                    // Each place gets the share of the tatweels that falls between where the place before it and this one end, so they are spread evenly.
                    var added = places == 0 ? 0 : (((found + 1) * count) / places) - ((found * count) / places);
                    written += FillTatweels(output, written, added);
                    found++;
                }

                joinsForward = type == JoiningType.Dual;
            }

            written += Copy(output, written, letter);
        }

        return found;
    }

    /// <summary>Writes a letter into a line, if there is a line to write it in.</summary>
    /// <param name="output">The line, or empty when places are only being counted.</param>
    /// <param name="at">Where in the line the letter goes.</param>
    /// <param name="letter">The letter.</param>
    /// <returns>1 when the letter was written, otherwise 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Copy(Span<char> output, int at, char letter)
    {
        if (output.IsEmpty)
        {
            return 0;
        }

        output[at] = letter;
        return 1;
    }

    /// <summary>Writes tatweels into a line, if there is a line to write them in.</summary>
    /// <param name="output">The line, or empty when places are only being counted.</param>
    /// <param name="start">Where in the line the tatweels go.</param>
    /// <param name="number">How many.</param>
    /// <returns>How many were written.</returns>
    private static int FillTatweels(Span<char> output, int start, int number)
    {
        if (output.IsEmpty)
        {
            return 0;
        }

        output.Slice(start, number).Fill(TatweelChar);
        return number;
    }

    /// <summary>A line and where tatweels go in it, as the state for making the stretched line.</summary>
    /// <param name="Text">The line.</param>
    /// <param name="Places">How many places it has.</param>
    /// <param name="Count">How many tatweels to add.</param>
    private readonly record struct Insertion(string Text, int Places, int Count);
}
