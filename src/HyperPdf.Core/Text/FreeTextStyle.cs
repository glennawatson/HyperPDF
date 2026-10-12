// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdf.Core.Text;

/// <summary>
/// The standard free text entries that describe how text looks: the default appearance (<c>/DA</c>), the default
/// style (<c>/DS</c>, CSS) and the rich text (<c>/RC</c>, XHTML). Writing them lets other readers show and edit the
/// text; reading them lets text written by other readers be edited here.
/// </summary>
public static class FreeTextStyle
{
    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>One channel's bits.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The largest channel value.</summary>
    private const float ChannelMax = 255;

    /// <summary>The CSS weight from which text counts as bold.</summary>
    private const int BoldWeight = 600;

    /// <summary>The characters a hex colour takes, with its hash.</summary>
    private const int HexColorChars = 7;

    /// <summary>The operands of a <c>k</c> (CMYK) colour.</summary>
    private const int CmykOperands = 4;

    /// <summary>The operands of an <c>rg</c> colour.</summary>
    private const int RgbOperands = 3;

    /// <summary>The operand of the yellow channel of a CMYK colour, counted from the end.</summary>
    private const int YellowFromEnd = 2;

    /// <summary>Converts a line height multiple to a percentage.</summary>
    private const float Percent = 100;

    /// <summary>The characters a style usually takes.</summary>
    private const int StyleChars = 160;

    /// <summary>The characters the rich text wrapping takes.</summary>
    private const int RichTextChars = 384;

    /// <summary>The CSS keyword for the regular weight and style.</summary>
    private const string Normal = "normal";

    /// <summary>The CSS keyword for italic.</summary>
    private const string Italic = "italic";

    /// <summary>The CSS keyword for bold.</summary>
    private const string Bold = "bold";

    /// <summary>The CSS properties read from a default style.</summary>
    private static readonly Dictionary<string, FormatSetter> Properties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["font-family"] = static (ref fields, value) => fields.FontFamily = Family(value) is { Length: > 0 } family ? family : fields.FontFamily,
        ["font-size"] = static (ref fields, value) => fields.FontSize = Size(value) is > 0 and var size ? size : fields.FontSize,
        ["font-weight"] = static (ref fields, value) => fields.IsBold = IsBoldWeight(value),
        ["font-style"] = static (ref fields, value) => fields.IsItalic = IsItalicStyle(value),
        ["text-align"] = static (ref fields, value) => fields.Alignment = Alignment(value),
        ["color"] = static (ref fields, value) => fields.Color = TryColor(value, out var color) ? color : fields.Color,
        ["text-decoration"] = static (ref fields, value) => fields.IsUnderline = value.Contains("underline", StringComparison.OrdinalIgnoreCase),
        ["letter-spacing"] = static (ref fields, value) => fields.CharacterSpacing = Size(value),
        ["font"] = Shorthand,
    };

    /// <summary>Writes the default appearance: a standard font resource, the size and the fill colour.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The content stream text.</returns>
    public static string DefaultAppearance(TextFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var resource = ResourceName(StandardFontFamilies.Closest(format.FontFamily, false, false));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"/{resource} {format.FontSize:0.###} Tf {Channel(format.Color, RedShift):0.###} {Channel(format.Color, GreenShift):0.###} {Channel(format.Color, 0):0.###} rg");
    }

    /// <summary>Writes the default style, a CSS declaration list.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The style.</returns>
    public static string DefaultStyle(TextFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var builder = new StringBuilder(StyleChars);
        _ = builder.Append("font-family:'").Append(format.FontFamily.Replace("'", string.Empty, StringComparison.Ordinal)).Append('\'')
            .Append(CultureInfo.InvariantCulture, $"; font-size:{format.FontSize:0.###}pt")
            .Append("; font-weight:").Append(format.IsBold ? Bold : Normal)
            .Append("; font-style:").Append(format.IsItalic ? Italic : Normal)
            .Append("; text-align:").Append(AlignmentName(format.Alignment))
            .Append(CultureInfo.InvariantCulture, $"; color:#{format.Color & 0xFFFFFFU:X6}");
        if (format.IsUnderline)
        {
            _ = builder.Append("; text-decoration:underline");
        }

        if (format.CharacterSpacing != 0)
        {
            _ = builder.Append(CultureInfo.InvariantCulture, $"; letter-spacing:{format.CharacterSpacing:0.###}pt");
        }

        return builder.Append(CultureInfo.InvariantCulture, $"; line-height:{format.LineSpacing * Percent:0.#}%").ToString();
    }

    /// <summary>Writes the rich text: an XHTML body with one paragraph per line.</summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The format.</param>
    /// <returns>The XHTML.</returns>
    public static string RichText(string text, TextFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        var builder = new StringBuilder(text.Length + RichTextChars);
        _ = builder.Append("<?xml version=\"1.0\"?><body xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:xfa=\"http://www.xfa.org/schema/xfa-data/1.0/\" ")
            .Append("xfa:APIVersion=\"Acrobat:11.0.0\" xfa:spec=\"2.0.2\" style=\"");
        AppendXml(builder, DefaultStyle(format));
        _ = builder.Append("\">");
        foreach (var line in text.AsSpan().EnumerateLines())
        {
            _ = builder.Append("<p>");
            AppendXml(builder, line);
            _ = builder.Append("</p>");
        }

        return builder.Append("</body>").ToString();
    }

    /// <summary>Applies what a default appearance says: the font size and colour.</summary>
    /// <param name="appearance">The default appearance.</param>
    /// <param name="format">The format to change.</param>
    /// <returns>The format with the size and colour found.</returns>
    public static TextFormat ApplyDefaultAppearance(string? appearance, TextFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (string.IsNullOrWhiteSpace(appearance))
        {
            return format;
        }

        Span<float> operands = stackalloc float[CmykOperands];
        var count = 0;
        var result = format;
        foreach (var range in appearance.AsSpan().SplitAny(" \t\r\n"))
        {
            var token = appearance.AsSpan(range);
            if (token.IsEmpty)
            {
                continue;
            }

            if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            {
                result = Operator(token, operands[..count], result);
                count = 0;
                continue;
            }

            if (count == operands.Length)
            {
                operands[1..].CopyTo(operands);
                count--;
            }

            operands[count] = number;
            count++;
        }

        return result;
    }

    /// <summary>Applies what a default style says: family, size, weight, slant, alignment and colour.</summary>
    /// <param name="style">The CSS declarations.</param>
    /// <param name="format">The format to change.</param>
    /// <returns>The format with what was found.</returns>
    public static TextFormat ApplyDefaultStyle(string? style, TextFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (string.IsNullOrWhiteSpace(style))
        {
            return format;
        }

        var lookup = Properties.GetAlternateLookup<ReadOnlySpan<char>>();
        var fields = new FormatFields(format);
        foreach (var range in style.AsSpan().Split(';'))
        {
            var declaration = style.AsSpan(range);
            var colon = declaration.IndexOf(':');
            if (colon > 0 && lookup.TryGetValue(declaration[..colon].Trim(), out var setter))
            {
                setter(ref fields, declaration[(colon + 1)..].Trim());
            }
        }

        return fields.ToFormat();
    }

    /// <summary>Gets the name PDF readers give a standard family's font resource.</summary>
    /// <param name="family">The standard family.</param>
    /// <returns>The resource name.</returns>
    private static string ResourceName(string family)
    {
        if (family == StandardFontFamilies.Serif)
        {
            return "TiRo";
        }

        return family == StandardFontFamilies.Mono ? "Cour" : "Helv";
    }

    /// <summary>Gets the CSS name of an alignment.</summary>
    /// <param name="alignment">The alignment.</param>
    /// <returns>The name.</returns>
    private static string AlignmentName(TextBoxAlignment alignment) => alignment switch
    {
        TextBoxAlignment.Center => "center",
        TextBoxAlignment.Right => "right",
        _ => "left",
    };

    /// <summary>Reads a CSS alignment.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The alignment; anything unknown is left.</returns>
    private static TextBoxAlignment Alignment(ReadOnlySpan<char> value)
    {
        if (value.Equals("center", StringComparison.OrdinalIgnoreCase))
        {
            return TextBoxAlignment.Center;
        }

        return value.Equals("right", StringComparison.OrdinalIgnoreCase) ? TextBoxAlignment.Right : TextBoxAlignment.Left;
    }

    /// <summary>Determines whether a CSS weight is bold.</summary>
    /// <param name="value">The weight.</param>
    /// <returns><see langword="true"/> for bold, bolder or 600 and above.</returns>
    private static bool IsBoldWeight(ReadOnlySpan<char> value) =>
        value.Equals(Bold, StringComparison.OrdinalIgnoreCase) || value.Equals("bolder", StringComparison.OrdinalIgnoreCase) || TextFormatCodec.Number(value, 0) >= BoldWeight;

    /// <summary>Determines whether a CSS style is slanted.</summary>
    /// <param name="value">The style.</param>
    /// <returns><see langword="true"/> for italic or oblique.</returns>
    private static bool IsItalicStyle(ReadOnlySpan<char> value) =>
        value.Equals(Italic, StringComparison.OrdinalIgnoreCase) || value.Equals("oblique", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads a CSS hex colour.</summary>
    /// <param name="value">The value, such as <c>#1A2B3C</c>.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when read.</returns>
    private static bool TryColor(ReadOnlySpan<char> value, out uint color)
    {
        color = 0;
        return value.Length == HexColorChars && value[0] == '#' && uint.TryParse(value[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out color);
    }

    /// <summary>Reads the CSS <c>font</c> shorthand that Acrobat writes, such as <c>bold italic 12.0pt Helvetica,sans-serif</c>.</summary>
    /// <param name="fields">The settings to change.</param>
    /// <param name="value">The value.</param>
    private static void Shorthand(ref FormatFields fields, ReadOnlySpan<char> value)
    {
        fields.IsBold = false;
        fields.IsItalic = false;
        var rest = value.TrimStart();
        while (!rest.IsEmpty)
        {
            var end = rest.IndexOf(' ');
            var token = end < 0 ? rest : rest[..end];
            if (!IsShorthandWord(token, ref fields))
            {
                // Everything from the family on is the family list.
                fields.FontFamily = Family(rest) is { Length: > 0 } family ? family : fields.FontFamily;
                return;
            }

            rest = end < 0 ? default : rest[(end + 1)..].TrimStart();
        }
    }

    /// <summary>Applies one word of the <c>font</c> shorthand that comes before the family.</summary>
    /// <param name="token">The word.</param>
    /// <param name="fields">The settings to change.</param>
    /// <returns><see langword="false"/> when the word starts the family list.</returns>
    private static bool IsShorthandWord(ReadOnlySpan<char> token, ref FormatFields fields)
    {
        if (token.Equals(Bold, StringComparison.OrdinalIgnoreCase))
        {
            fields.IsBold = true;
            return true;
        }

        if (IsItalicStyle(token))
        {
            fields.IsItalic = true;
            return true;
        }

        if (token.Equals(Normal, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!token.IsEmpty && (char.IsAsciiDigit(token[0]) || token[0] == '.') && Size(token) is > 0 and var size)
        {
            fields.FontSize = size;
            return true;
        }

        return false;
    }

    /// <summary>Reads the first family of a CSS family list, without quotes; generic families map to the standard ones.</summary>
    /// <param name="value">The list.</param>
    /// <returns>The family, or an empty string.</returns>
    private static string Family(ReadOnlySpan<char> value)
    {
        var comma = value.IndexOf(',');
        var first = (comma < 0 ? value : value[..comma]).Trim().Trim('\'').Trim('"').Trim();
        if (first.Equals("sans-serif", StringComparison.OrdinalIgnoreCase))
        {
            return StandardFontFamilies.Sans;
        }

        if (first.Equals("serif", StringComparison.OrdinalIgnoreCase))
        {
            return StandardFontFamilies.Serif;
        }

        return first.Equals("monospace", StringComparison.OrdinalIgnoreCase) ? StandardFontFamilies.Mono : first.ToString();
    }

    /// <summary>Reads a CSS length in points; pixels count as points, as PDF readers treat them.</summary>
    /// <param name="value">The length, such as <c>12pt</c>.</param>
    /// <returns>The length, or 0.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Size(ReadOnlySpan<char> value) => TextFormatCodec.Number(value.TrimEnd("ptxPTX "), 0);

    /// <summary>Applies a content stream operator to the format.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="operands">Its operands.</param>
    /// <param name="format">The format.</param>
    /// <returns>The changed format.</returns>
    private static TextFormat Operator(ReadOnlySpan<char> op, ReadOnlySpan<float> operands, TextFormat format)
    {
        if (op is "Tf" && !operands.IsEmpty && operands[^1] > 0)
        {
            return format with { FontSize = operands[^1] };
        }

        if (op is "g" && !operands.IsEmpty)
        {
            return format with { Color = Rgb(operands[^1], operands[^1], operands[^1]) };
        }

        if (op is "rg" && operands.Length >= RgbOperands)
        {
            return format with { Color = Rgb(operands[^RgbOperands], operands[^YellowFromEnd], operands[^1]) };
        }

        if (op is "k" && operands.Length >= CmykOperands)
        {
            var white = 1 - operands[^1];
            return format with { Color = Rgb((1 - operands[^CmykOperands]) * white, (1 - operands[^RgbOperands]) * white, (1 - operands[^YellowFromEnd]) * white) };
        }

        return format;
    }

    /// <summary>Packs three 0 to 1 channels as 0xRRGGBB.</summary>
    /// <param name="red">The red channel.</param>
    /// <param name="green">The green channel.</param>
    /// <param name="blue">The blue channel.</param>
    /// <returns>The colour.</returns>
    private static uint Rgb(float red, float green, float blue) => (Byte(red) << RedShift) | (Byte(green) << GreenShift) | Byte(blue);

    /// <summary>Converts a 0 to 1 channel to a byte.</summary>
    /// <param name="value">The channel.</param>
    /// <returns>The byte.</returns>
    private static uint Byte(float value) => (uint)Math.Clamp(MathF.Round(value * ChannelMax), 0, ChannelMax);

    /// <summary>Gets one channel of a colour as 0 to 1.</summary>
    /// <param name="color">The colour.</param>
    /// <param name="shift">The channel's bit offset.</param>
    /// <returns>The channel.</returns>
    private static float Channel(uint color, int shift) => ((color >> shift) & ChannelMask) / ChannelMax;

    /// <summary>Appends text with XML's special characters escaped.</summary>
    /// <param name="builder">The builder.</param>
    /// <param name="text">The text.</param>
    private static void AppendXml(StringBuilder builder, ReadOnlySpan<char> text)
    {
        foreach (var c in text)
        {
            _ = c switch
            {
                '<' => builder.Append("&lt;"),
                '>' => builder.Append("&gt;"),
                '&' => builder.Append("&amp;"),
                '"' => builder.Append("&quot;"),
                _ => builder.Append(c),
            };
        }
    }
}
