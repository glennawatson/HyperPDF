// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Finds the labels that start list items: bullets, numbers such as <c>1.</c> or <c>(2)</c>, and letters or Roman numerals
/// such as <c>a)</c> or <c>iv.</c>. A label is the first word of a line, set apart from the text that follows. A run of
/// labels is a list only when its bullets match, or its numbers count up by one.
/// </summary>
internal static class LayoutListLabels
{
    /// <summary>The most characters a label has.</summary>
    private const int MaxLabel = 6;

    /// <summary>The gap after a label, in line heights, that sets it apart from the text.</summary>
    private const float LabelGap = 0.15F;

    /// <summary>The most digits in a number label.</summary>
    private const int MaxDigits = 3;

    /// <summary>The first and last code points of the private use area, where symbol fonts put their bullets.</summary>
    private const char PrivateUseStart = '';

    /// <summary>The end of the private use area.</summary>
    private const char PrivateUseEnd = '';

    /// <summary>The number of letters in the alphabet.</summary>
    private const int Alphabet = 26;

    /// <summary>The characters that are filled round bullets.</summary>
    private static readonly SearchValues<char> DiscBullets = SearchValues.Create("•●·‣⁃*");

    /// <summary>The characters that are hollow round bullets.</summary>
    private static readonly SearchValues<char> CircleBullets = SearchValues.Create("○◦∘");

    /// <summary>The characters that are square bullets.</summary>
    private static readonly SearchValues<char> SquareBullets = SearchValues.Create("▪■□⬛");

    /// <summary>The dashes used as bullets.</summary>
    private static readonly SearchValues<char> DashBullets = SearchValues.Create("-–—−");

    /// <summary>The value of the Roman numerals I, V, X, L, C, D and M in that order.</summary>
    private static readonly int[] RomanValues = [1, 5, 10, 50, 100, 500, 1000];

    /// <summary>Measures the label at the start of a line.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="items">The items.</param>
    /// <param name="line">The line, whose items run left to right.</param>
    /// <returns>The label; one with no items when the line has none.</returns>
    internal static ListLabel Measure(string text, LayoutItem[] items, in LayoutLine line)
    {
        if (line.Kind != LayoutLineKind.Text || line.Count < 2 || !MayStartLabel(text, items[line.Start]))
        {
            return default;
        }

        Span<char> token = stackalloc char[MaxLabel];
        var read = ReadToken(text, items, line, token, out var length, out var taken);
        return !read || taken >= line.Count || length == 0 ? default : Classify(token[..length], taken, items[line.Start + taken].Bounds.Left);
    }

    /// <summary>Works out what kind of list a run of labels makes.</summary>
    /// <param name="labels">The labels of consecutive items.</param>
    /// <returns>The numbering, or <see langword="null"/> when the labels do not make a list.</returns>
    internal static PdfListNumbering? Resolve(ReadOnlySpan<ListLabel> labels)
    {
        if (labels.IsEmpty)
        {
            return null;
        }

        if (labels[0].IsBullet)
        {
            foreach (var label in labels)
            {
                if (!label.IsBullet)
                {
                    return null;
                }
            }

            return labels[0].Kind;
        }

        if (CountsUp(labels, false))
        {
            return labels[0].Kind;
        }

        if (CountsUp(labels, true))
        {
            return IsUpper(labels[0].Kind) ? PdfListNumbering.UpperRoman : PdfListNumbering.LowerRoman;
        }

        return null;
    }

    /// <summary>Reads the first word of a line: items up to a space or a gap.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="items">The items.</param>
    /// <param name="line">The line.</param>
    /// <param name="token">Receives the word's characters.</param>
    /// <param name="length">Receives the word's length.</param>
    /// <param name="taken">Receives the number of items the word and its space take.</param>
    /// <returns><see langword="false"/> when the word is too long to be a label.</returns>
    private static bool ReadToken(string text, LayoutItem[] items, in LayoutLine line, Span<char> token, out int length, out int taken)
    {
        length = 0;
        taken = 0;
        var limit = LabelGap * line.Bounds.Height;
        while (taken < line.Count)
        {
            var item = items[line.Start + taken];
            var piece = text.AsSpan(item.TextStart, item.TextLength);
            taken++;
            if (piece.IsWhiteSpace())
            {
                return true;
            }

            if (length + piece.Length > token.Length)
            {
                return false;
            }

            piece.CopyTo(token[length..]);
            length += piece.Length;
            if (taken < line.Count && items[line.Start + taken].Bounds.Left - item.Bounds.Right > limit)
            {
                return true;
            }
        }

        return true;
    }

    /// <summary>Determines whether a first character could start a label, so most lines are rejected at once.</summary>
    /// <param name="text">The items' text.</param>
    /// <param name="first">The line's first item.</param>
    /// <returns><see langword="true"/> for a character a label may start with.</returns>
    private static bool MayStartLabel(string text, in LayoutItem first)
    {
        if (first.TextLength == 0)
        {
            return false;
        }

        var character = text[first.TextStart];
        return char.IsLetterOrDigit(character) || character == '(' || IsBullet(character) || character is >= PrivateUseStart and <= PrivateUseEnd;
    }

    /// <summary>Determines whether a character is a bullet or a dash used as one.</summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for a bullet.</returns>
    private static bool IsBullet(char character) => BulletKind(character) is not null;

    /// <summary>Gets the shape of a bullet character.</summary>
    /// <param name="character">The character.</param>
    /// <returns>The shape, or <see langword="null"/> when the character is not a bullet.</returns>
    private static PdfListNumbering? BulletKind(char character)
    {
        if (DiscBullets.Contains(character) || character is >= PrivateUseStart and <= PrivateUseEnd)
        {
            return PdfListNumbering.Disc;
        }

        if (CircleBullets.Contains(character))
        {
            return PdfListNumbering.Circle;
        }

        if (SquareBullets.Contains(character))
        {
            return PdfListNumbering.Square;
        }

        return DashBullets.Contains(character) ? PdfListNumbering.None : null;
    }

    /// <summary>Classifies the first word of a line as a label.</summary>
    /// <param name="token">The word.</param>
    /// <param name="taken">The items it takes.</param>
    /// <param name="bodyLeft">The left edge of the text after it.</param>
    /// <returns>The label, or one with no items when the word is not a label.</returns>
    private static ListLabel Classify(ReadOnlySpan<char> token, int taken, float bodyLeft)
    {
        if (token.Length == 1 && BulletKind(token[0]) is { } shape)
        {
            return new(taken, shape, true, 0, 0, bodyLeft);
        }

        var body = Unwrap(token);
        if (body.IsEmpty)
        {
            return default;
        }

        return IsDecimal(body)
            ? new(taken, PdfListNumbering.Decimal, false, int.Parse(body, System.Globalization.CultureInfo.InvariantCulture), 0, bodyLeft)
            : ClassifyLetters(body, taken, bodyLeft);
    }

    /// <summary>Classifies a word of letters as an alphabet letter or a Roman numeral.</summary>
    /// <param name="body">The letters, without their closing mark.</param>
    /// <param name="taken">The items the label takes.</param>
    /// <param name="bodyLeft">The left edge of the text after it.</param>
    /// <returns>The label, or one with no items when the letters are neither.</returns>
    private static ListLabel ClassifyLetters(ReadOnlySpan<char> body, int taken, float bodyLeft)
    {
        var upper = char.IsUpper(body[0]);
        foreach (var letter in body)
        {
            if (!char.IsAsciiLetter(letter) || char.IsUpper(letter) != upper)
            {
                return default;
            }
        }

        var roman = RomanValue(body);
        if (body.Length == 1)
        {
            var place = char.ToLowerInvariant(body[0]) - 'a' + 1;
            return new(taken, upper ? PdfListNumbering.UpperAlpha : PdfListNumbering.LowerAlpha, false, place, roman, bodyLeft);
        }

        return roman > 0 ? new(taken, upper ? PdfListNumbering.UpperRoman : PdfListNumbering.LowerRoman, false, roman, roman, bodyLeft) : default;
    }

    /// <summary>Takes the closing mark or the brackets off a label word, leaving what counts.</summary>
    /// <param name="token">The word, such as <c>1.</c>, <c>2)</c> or <c>(a)</c>.</param>
    /// <returns>The word without them; empty when the word has no such mark.</returns>
    private static ReadOnlySpan<char> Unwrap(ReadOnlySpan<char> token)
    {
        if (token.Length >= 3 && token[0] == '(' && token[^1] == ')')
        {
            return token[1..^1];
        }

        return token.Length >= 2 && token[^1] is '.' or ')' ? token[..^1] : default;
    }

    /// <summary>Determines whether a word is a short whole number.</summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> for one to three digits.</returns>
    private static bool IsDecimal(ReadOnlySpan<char> word)
    {
        if (word.Length > MaxDigits)
        {
            return false;
        }

        foreach (var digit in word)
        {
            if (digit is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads a Roman numeral.</summary>
    /// <param name="letters">The letters, all of one case.</param>
    /// <returns>The value, or zero when the letters are not a numeral.</returns>
    private static int RomanValue(ReadOnlySpan<char> letters)
    {
        var total = 0;
        var previous = 0;
        for (var i = letters.Length - 1; i >= 0; i--)
        {
            var value = RomanDigit(letters[i]);
            if (value == 0)
            {
                return 0;
            }

            total += value < previous ? -value : value;
            previous = Math.Max(previous, value);
        }

        return total;
    }

    /// <summary>Gets the value of a Roman numeral letter.</summary>
    /// <param name="letter">The letter.</param>
    /// <returns>Its value, or zero for another letter.</returns>
    private static int RomanDigit(char letter)
    {
        var index = "ivxlcdm".IndexOf(char.ToLowerInvariant(letter));
        return index < 0 ? 0 : RomanValues[index];
    }

    /// <summary>Determines whether the labels count up by one, as numbers or letters, or as Roman numerals.</summary>
    /// <param name="labels">The labels.</param>
    /// <param name="roman">Whether to read them as Roman numerals.</param>
    /// <returns><see langword="true"/> when each is one more than the one before.</returns>
    private static bool CountsUp(ReadOnlySpan<ListLabel> labels, bool roman)
    {
        var previous = 0;
        for (var i = 0; i < labels.Length; i++)
        {
            var value = roman ? labels[i].Roman : labels[i].Number;
            if (!Fits(labels[i], labels[0], roman, value) || (i > 0 && value != previous + 1))
            {
                return false;
            }

            previous = value;
        }

        return true;
    }

    /// <summary>Determines whether a label can sit in a list that starts with another label.</summary>
    /// <param name="label">The label.</param>
    /// <param name="first">The list's first label.</param>
    /// <param name="roman">Whether the labels are read as Roman numerals.</param>
    /// <param name="value">The label's value as read.</param>
    /// <returns><see langword="true"/> when it has a value, the same kind and a plausible size.</returns>
    private static bool Fits(in ListLabel label, in ListLabel first, bool roman, int value)
    {
        var sameKind = roman ? IsUpper(label.Kind) == IsUpper(first.Kind) : label.Kind == first.Kind;
        var inRange = roman || label.Kind == PdfListNumbering.Decimal || value <= Alphabet;
        return value > 0 && sameKind && inRange;
    }

    /// <summary>Determines whether a kind of label is in upper case.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for upper case letters and numerals.</returns>
    private static bool IsUpper(PdfListNumbering kind) => kind is PdfListNumbering.UpperAlpha or PdfListNumbering.UpperRoman;
}
