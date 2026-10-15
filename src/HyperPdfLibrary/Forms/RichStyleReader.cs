// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Globalization;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Reads the CSS declarations of a rich text <c>style</c> attribute or a default style (<c>/DS</c>), the properties of
/// ISO 32000-2 12.7.4.3 that change how text looks. Properties it does not know are ignored.
/// </summary>
internal static class RichStyleReader
{
    /// <summary>The points in a CSS pixel.</summary>
    private const float PointsPerPixel = 0.75F;

    /// <summary>The CSS weight from which text counts as bold.</summary>
    private const int BoldWeight = 600;

    /// <summary>The longest hex colour: <c>#rrggbb</c>.</summary>
    private const int LongHexLength = 6;

    /// <summary>The shortest hex colour: <c>#rgb</c>.</summary>
    private const int ShortHexLength = 3;

    /// <summary>The length of <c>rgb(</c>.</summary>
    private const int FunctionPrefixLength = 4;

    /// <summary>The factor that turns a one digit channel into a two digit one: <c>0xF * 0x11 = 0xFF</c>.</summary>
    private const int ShortChannelScale = 0x11;

    /// <summary>The bits in a colour channel.</summary>
    private const int ChannelBits = 8;

    /// <summary>The bits to shift red up by in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bits to shift green up by in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The most a channel can be.</summary>
    private const int ChannelMax = 255;

    /// <summary>The channels of a functional colour.</summary>
    private const int Channels = 3;

    /// <summary>The alignment value for centred text.</summary>
    private const int CenterAlignment = 1;

    /// <summary>The alignment value for right aligned text.</summary>
    private const int RightAlignment = 2;

    /// <summary>The words in a CSS family name that mean a fixed width font.</summary>
    private static readonly string[] MonoWords = ["courier", "mono", "consolas"];

    /// <summary>The words in a CSS family name that mean a serif font.</summary>
    private static readonly string[] SerifWords = ["times", "serif", "georgia", "garamond"];

    /// <summary>The setters by property name.</summary>
    private static readonly FrozenDictionary<string, RichStyleSetter> Setters = new Dictionary<string, RichStyleSetter>(StringComparer.OrdinalIgnoreCase)
    {
        ["font-family"] = static (style, value) => style with { Family = ReadFamily(value) },
        ["font-size"] = static (style, value) => TryReadSize(value, out var size) ? style with { Size = size } : style,
        ["font-weight"] = static (style, value) => style with { Bold = IsBold(value) },
        ["font-style"] = static (style, value) => style with { Italic = IsItalic(value) },
        ["text-decoration"] = ReadDecoration,
        ["text-align"] = static (style, value) => style with { Alignment = ReadAlignment(value, style.Alignment) },
        ["color"] = static (style, value) => TryReadColor(value, out var color) ? style with { Color = color } : style,
        ["xfa-spacerun"] = static (style, value) => style with { KeepSpaces = value.Equals("yes", StringComparison.OrdinalIgnoreCase) },
        ["font"] = ReadShorthand,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The colour keywords the subset uses.</summary>
    private static readonly FrozenDictionary<string, int> NamedColors = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = 0x000000,
        ["white"] = 0xFFFFFF,
        ["red"] = 0xFF0000,
        ["green"] = 0x008000,
        ["blue"] = 0x0000FF,
        ["yellow"] = 0xFFFF00,
        ["gray"] = 0x808080,
        ["grey"] = 0x808080,
        ["silver"] = 0xC0C0C0,
        ["maroon"] = 0x800000,
        ["navy"] = 0x000080,
        ["purple"] = 0x800080,
        ["teal"] = 0x008080,
        ["olive"] = 0x808000,
        ["lime"] = 0x00FF00,
        ["aqua"] = 0x00FFFF,
        ["fuchsia"] = 0xFF00FF,
        ["orange"] = 0xFFA500,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>Applies a list of CSS declarations to a style.</summary>
    /// <param name="css">The declarations, such as <c>font-size:12pt; color:#FF0000</c>; may be empty.</param>
    /// <param name="style">The style to start from.</param>
    /// <returns>The style with the declarations applied.</returns>
    internal static RichStyle Apply(ReadOnlySpan<char> css, RichStyle style)
    {
        var lookup = Setters.GetAlternateLookup<ReadOnlySpan<char>>();
        var result = style;
        foreach (var range in css.Split(';'))
        {
            var declaration = css[range];
            var colon = declaration.IndexOf(':');
            if (colon > 0 && lookup.TryGetValue(declaration[..colon].Trim(), out var setter))
            {
                result = setter(result, declaration[(colon + 1)..].Trim());
            }
        }

        return result;
    }

    /// <summary>Reads a font size: a number with <c>pt</c>, <c>px</c> or no unit.</summary>
    /// <param name="value">The value.</param>
    /// <param name="size">Receives the size in points.</param>
    /// <returns><see langword="true"/> when the value is a positive size.</returns>
    internal static bool TryReadSize(ReadOnlySpan<char> value, out float size)
    {
        var pixels = value.EndsWith("px", StringComparison.OrdinalIgnoreCase);
        var number = value.TrimEnd("ptx").Trim();
        var parsed = float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out size) && size > 0 && float.IsFinite(size);
        size = pixels ? size * PointsPerPixel : size;
        return parsed;
    }

    /// <summary>Reads a colour: <c>#rgb</c>, <c>#rrggbb</c>, <c>rgb(r, g, b)</c> or a keyword.</summary>
    /// <param name="value">The value.</param>
    /// <param name="color">Receives the colour as 0xRRGGBB.</param>
    /// <returns><see langword="true"/> when the value is a colour.</returns>
    internal static bool TryReadColor(ReadOnlySpan<char> value, out int color)
    {
        color = 0;
        if (value.StartsWith("#", StringComparison.Ordinal))
        {
            return TryReadHexColor(value[1..], out color);
        }

        return value.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(")", StringComparison.Ordinal)
            ? TryReadFunctionalColor(value[FunctionPrefixLength..^1], out color)
            : NamedColors.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(value, out color);
    }

    /// <summary>Reads the text decoration: underline and line-through.</summary>
    /// <param name="style">The style so far.</param>
    /// <param name="value">The value.</param>
    /// <returns>The style with the decoration applied.</returns>
    private static RichStyle ReadDecoration(RichStyle style, ReadOnlySpan<char> value) => style with
    {
        Underline = value.Contains("underline", StringComparison.OrdinalIgnoreCase),
        StrikeOut = value.Contains("line-through", StringComparison.OrdinalIgnoreCase),
    };

    /// <summary>Reads a font family list, keeping the kind of the first family.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The kind of font the first family names.</returns>
    private static RichFamily ReadFamily(ReadOnlySpan<char> value)
    {
        var comma = value.IndexOf(',');
        var first = comma < 0 ? value : value[..comma];
        var name = first.Trim().Trim("'\"");
        return name.IsEmpty ? RichFamily.Inherit : Classify(name, false, false, false);
    }

    /// <summary>Finds the standard family closest to a CSS family name.</summary>
    /// <param name="family">The family name; may be empty.</param>
    /// <param name="mono">Whether an earlier word of the name was a fixed width family.</param>
    /// <param name="serif">Whether an earlier word of the name was a serif family.</param>
    /// <param name="sans">Whether an earlier word of the name was a sans serif family.</param>
    /// <returns>The family; <see cref="RichFamily.Inherit"/> when the name and the earlier words are empty.</returns>
    private static RichFamily Classify(ReadOnlySpan<char> family, bool mono, bool serif, bool sans)
    {
        var isMono = mono || ContainsAny(family, MonoWords);
        var isSerif = serif || ContainsAny(family, SerifWords);
        var isSans = sans || family.Contains("sans", StringComparison.OrdinalIgnoreCase);
        if (isMono)
        {
            return RichFamily.Mono;
        }

        return isSerif && !isSans ? RichFamily.Serif : RichFamily.Sans;
    }

    /// <summary>Determines whether a name contains any of some words, ignoring case.</summary>
    /// <param name="name">The name.</param>
    /// <param name="words">The words.</param>
    /// <returns><see langword="true"/> when one is found.</returns>
    private static bool ContainsAny(ReadOnlySpan<char> name, string[] words)
    {
        foreach (var word in words)
        {
            if (name.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a font weight is bold.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for <c>bold</c>, <c>bolder</c> or a weight of 600 or more.</returns>
    private static bool IsBold(ReadOnlySpan<char> value) =>
        value.StartsWith("bold", StringComparison.OrdinalIgnoreCase)
        || (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var weight) && weight >= BoldWeight);

    /// <summary>Determines whether a font style is slanted.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for <c>italic</c> and <c>oblique</c>.</returns>
    private static bool IsItalic(ReadOnlySpan<char> value) =>
        value.Equals("italic", StringComparison.OrdinalIgnoreCase) || value.Equals("oblique", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads a text alignment.</summary>
    /// <param name="value">The value.</param>
    /// <param name="current">The alignment to keep for a value that is not known.</param>
    /// <returns>The alignment: 0 left, 1 centred, 2 right.</returns>
    private static int ReadAlignment(ReadOnlySpan<char> value, int current)
    {
        if (value.Equals("center", StringComparison.OrdinalIgnoreCase))
        {
            return CenterAlignment;
        }

        if (value.Equals("right", StringComparison.OrdinalIgnoreCase))
        {
            return RightAlignment;
        }

        return value.Equals("left", StringComparison.OrdinalIgnoreCase) || value.Equals("justify", StringComparison.OrdinalIgnoreCase) ? 0 : current;
    }

    /// <summary>Reads the <c>font</c> shorthand: style and weight words, a size and a family, as in <c>bold 12pt Helvetica</c>.</summary>
    /// <param name="style">The style so far.</param>
    /// <param name="value">The value.</param>
    /// <returns>The style with what was found.</returns>
    private static RichStyle ReadShorthand(RichStyle style, ReadOnlySpan<char> value)
    {
        var result = style;
        var named = false;
        var mono = false;
        var serif = false;
        var sans = false;
        foreach (var range in value.Split(' '))
        {
            var word = value[range].Trim();
            if (word.IsEmpty || word.Equals("normal", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (word.Equals("bold", StringComparison.OrdinalIgnoreCase))
            {
                result = result with { Bold = true };
            }
            else if (IsItalic(word))
            {
                result = result with { Italic = true };
            }
            else if (char.IsAsciiDigit(word[0]) && TryReadSize(word, out var size))
            {
                result = result with { Size = size };
            }
            else
            {
                var part = word.Trim("'\",");
                named |= !part.IsEmpty;
                mono |= ContainsAny(part, MonoWords);
                serif |= ContainsAny(part, SerifWords);
                sans |= part.Contains("sans", StringComparison.OrdinalIgnoreCase);
            }
        }

        return named ? result with { Family = Classify(default, mono, serif, sans) } : result;
    }

    /// <summary>Reads the digits of a hex colour.</summary>
    /// <param name="digits">The digits after the hash.</param>
    /// <param name="color">Receives the colour.</param>
    /// <returns><see langword="true"/> when there are three or six hex digits.</returns>
    private static bool TryReadHexColor(ReadOnlySpan<char> digits, out int color)
    {
        color = 0;
        if (digits.Length is not (ShortHexLength or LongHexLength)
            || !int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        color = digits.Length == LongHexLength
            ? number
            : (Doubled(digits[0]) << RedShift) | (Doubled(digits[1]) << GreenShift) | Doubled(digits[2]);
        return true;
    }

    /// <summary>Turns one hex digit of a short colour into the channel it stands for: <c>f</c> is <c>ff</c>.</summary>
    /// <param name="digit">The hex digit.</param>
    /// <returns>The channel value.</returns>
    private static int Doubled(char digit) => int.Parse(new ReadOnlySpan<char>(in digit), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture) * ShortChannelScale;

    /// <summary>Reads the three numbers of <c>rgb(r, g, b)</c>.</summary>
    /// <param name="arguments">The text between the brackets.</param>
    /// <param name="color">Receives the colour.</param>
    /// <returns><see langword="true"/> when there are three numbers from 0 to 255.</returns>
    private static bool TryReadFunctionalColor(ReadOnlySpan<char> arguments, out int color)
    {
        color = 0;
        var count = 0;
        foreach (var range in arguments.Split(','))
        {
            if (count == Channels || !int.TryParse(arguments[range].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var channel) || channel > ChannelMax)
            {
                return false;
            }

            color = (color << ChannelBits) | channel;
            count++;
        }

        return count == Channels;
    }
}
