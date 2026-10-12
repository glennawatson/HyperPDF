// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Text;

/// <summary>
/// Writes a text format and wrap width as the compact record kept with a text box written here, and reads it back.
/// The record keeps every setting, including those the standard free text entries cannot hold.
/// </summary>
public static class TextFormatCodec
{
    /// <summary>The version tag starting the record.</summary>
    private const string Version = "v1;";

    /// <summary>The key of the wrap width, which is not part of the format.</summary>
    private const string WrapKey = "wrap";

    /// <summary>How numbers are written: up to three decimals.</summary>
    private const string NumberFormat = "0.###";

    /// <summary>How the colour is written: six hexadecimal digits.</summary>
    private const string ColorFormat = "X6";

    /// <summary>The characters a record usually takes.</summary>
    private const int RecordChars = 160;

    /// <summary>The characters a hexadecimal UTF-16 code unit takes.</summary>
    private const int HexUnitChars = 4;

    /// <summary>The characters around a hexadecimal text string: the brackets and the byte order mark.</summary>
    private const int HexFrameChars = 6;

    /// <summary>The bits one hexadecimal digit holds; also the shift to the third digit of a code unit.</summary>
    private const int HexDigitBits = 4;

    /// <summary>The shift to the first, highest, digit of a code unit.</summary>
    private const int FirstDigitShift = 12;

    /// <summary>The shift to the second digit of a code unit.</summary>
    private const int SecondDigitShift = 8;

    /// <summary>The end of a hexadecimal text string.</summary>
    private const char HexEnd = '>';

    /// <summary>The start of a hexadecimal text string: the bracket and the UTF-16 byte order mark.</summary>
    private const string HexStart = "<FEFF";

    /// <summary>The upper case hexadecimal digits.</summary>
    private const string HexDigits = "0123456789ABCDEF";

    /// <summary>The setting each key of the record changes.</summary>
    private static readonly Dictionary<string, FormatSetter> Setters = new(StringComparer.Ordinal)
    {
        ["family"] = static (ref fields, value) => fields.FontFamily = Unescape(value),
        ["size"] = static (ref fields, value) => fields.FontSize = Number(value, fields.FontSize),
        ["color"] = static (ref fields, value) => fields.Color = uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var color) ? color : fields.Color,
        ["bold"] = static (ref fields, value) => fields.IsBold = value is "1",
        ["italic"] = static (ref fields, value) => fields.IsItalic = value is "1",
        ["underline"] = static (ref fields, value) => fields.IsUnderline = value is "1",
        ["align"] = static (ref fields, value) => fields.Alignment = (TextBoxAlignment)(int)Number(value, 0),
        ["line"] = static (ref fields, value) => fields.LineSpacing = Number(value, fields.LineSpacing),
        ["char"] = static (ref fields, value) => fields.CharacterSpacing = Number(value, 0),
        ["comb"] = static (ref fields, value) => fields.CombCells = (int)Number(value, 0),
    };

    /// <summary>Writes the record of a format and wrap width.</summary>
    /// <param name="format">The format.</param>
    /// <param name="wrapWidth">The wrap width in points, or 0.</param>
    /// <returns>The record.</returns>
    public static string Write(TextFormat format, float wrapWidth)
    {
        ArgumentNullException.ThrowIfNull(format);
        var record = new DefaultInterpolatedStringHandler(0, 0, CultureInfo.InvariantCulture, stackalloc char[RecordChars]);
        record.AppendLiteral(Version);
        record.AppendLiteral("family=");
        record.AppendFormatted(Escape(format.FontFamily));
        record.AppendLiteral(";size=");
        record.AppendFormatted(format.FontSize, NumberFormat);
        record.AppendLiteral(";color=");
        record.AppendFormatted(format.Color & 0xFFFFFFU, ColorFormat);
        record.AppendLiteral(";bold=");
        record.AppendFormatted(Flag(format.IsBold));
        record.AppendLiteral(";italic=");
        record.AppendFormatted(Flag(format.IsItalic));
        record.AppendLiteral(";underline=");
        record.AppendFormatted(Flag(format.IsUnderline));
        record.AppendLiteral(";align=");
        record.AppendFormatted((int)format.Alignment);
        record.AppendLiteral(";line=");
        record.AppendFormatted(format.LineSpacing, NumberFormat);
        record.AppendLiteral(";char=");
        record.AppendFormatted(format.CharacterSpacing, NumberFormat);
        record.AppendLiteral(";comb=");
        record.AppendFormatted(format.CombCells);
        record.AppendLiteral(";wrap=");
        record.AppendFormatted(wrapWidth, NumberFormat);
        return record.ToStringAndClear();
    }

    /// <summary>Reads a record back. Unknown keys are skipped, so newer records still read.</summary>
    /// <param name="record">The record.</param>
    /// <param name="format">The format read, clamped to usable values.</param>
    /// <param name="wrapWidth">The wrap width read.</param>
    /// <returns><see langword="true"/> when the record was read.</returns>
    public static bool TryRead(ReadOnlySpan<char> record, out TextFormat format, out float wrapWidth)
    {
        format = TextFormat.Default;
        wrapWidth = 0;
        if (!record.StartsWith(Version, StringComparison.Ordinal))
        {
            return false;
        }

        var lookup = Setters.GetAlternateLookup<ReadOnlySpan<char>>();
        var fields = new FormatFields(TextFormat.Default);
        var body = record[Version.Length..];
        foreach (var range in body.Split(';'))
        {
            var part = body[range];
            var equals = part.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var key = part[..equals];
            var value = part[(equals + 1)..];
            if (lookup.TryGetValue(key, out var setter))
            {
                setter(ref fields, value);
            }
            else if (key is WrapKey)
            {
                wrapWidth = Math.Max(0, Number(value, 0));
            }
        }

        format = fields.ToFormat().Clamped();
        return true;
    }

    /// <summary>Encodes text as a PDF text string in hexadecimal UTF-16, which is plain ASCII.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The string, such as <c>&lt;FEFF0041&gt;</c>.</returns>
    public static string HexTextString(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Create(HexTextStringLength(text.Length), text, static (destination, value) => WriteHex(value, destination));
    }

    /// <summary>Gets the length of the hexadecimal text string of text.</summary>
    /// <param name="textLength">The text's length.</param>
    /// <returns>The characters the string takes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int HexTextStringLength(int textLength) => (textLength * HexUnitChars) + HexFrameChars;

    /// <summary>Writes text as a PDF text string in hexadecimal UTF-16 into a span, with no string in between.</summary>
    /// <param name="text">The text.</param>
    /// <param name="destination">The span, at least <see cref="HexTextStringLength"/> long.</param>
    /// <returns>The characters written.</returns>
    public static int WriteHexTextString(ReadOnlySpan<char> text, Span<char> destination)
    {
        var length = HexTextStringLength(text.Length);
        WriteHex(text, destination[..length]);
        return length;
    }

    /// <summary>Parses a number, or returns a fallback.</summary>
    /// <param name="value">The text.</param>
    /// <param name="fallback">The fallback.</param>
    /// <returns>The number.</returns>
    internal static float Number(ReadOnlySpan<char> value, float fallback) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && float.IsFinite(number) ? number : fallback;

    /// <summary>Writes the hexadecimal text string of text into a span of exactly its length.</summary>
    /// <param name="text">The text.</param>
    /// <param name="destination">The span.</param>
    private static void WriteHex(ReadOnlySpan<char> text, Span<char> destination)
    {
        HexStart.CopyTo(destination);
        var at = HexStart.Length;
        foreach (var c in text)
        {
            for (var shift = FirstDigitShift; shift >= 0; shift -= HexDigitBits)
            {
                destination[at] = HexDigit(c >> shift);
                at++;
            }
        }

        destination[at] = HexEnd;
    }

    /// <summary>Gets the upper case hexadecimal digit of the lowest four bits of a value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The digit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char HexDigit(int value) => HexDigits[value & 0xF];

    /// <summary>Writes a setting that is on or off.</summary>
    /// <param name="value">The setting.</param>
    /// <returns>1 or 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static char Flag(bool value) => value ? '1' : '0';

    /// <summary>Escapes the separators of the record.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The escaped value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Escape(string value) =>
        value.Replace("%", "%25", StringComparison.Ordinal).Replace(";", "%3B", StringComparison.Ordinal).Replace("=", "%3D", StringComparison.Ordinal);

    /// <summary>Reverses <see cref="Escape"/>.</summary>
    /// <param name="value">The escaped value.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Unescape(ReadOnlySpan<char> value)
    {
        // The built-in families are kept as they are, so reading them makes no new string.
        if (value.SequenceEqual(StandardFontFamilies.Sans))
        {
            return StandardFontFamilies.Sans;
        }

        if (value.SequenceEqual(StandardFontFamilies.Serif))
        {
            return StandardFontFamilies.Serif;
        }

        return value.SequenceEqual(StandardFontFamilies.Mono)
            ? StandardFontFamilies.Mono
            : value.ToString().Replace("%3B", ";", StringComparison.Ordinal).Replace("%3D", "=", StringComparison.Ordinal).Replace("%25", "%", StringComparison.Ordinal);
    }
}
