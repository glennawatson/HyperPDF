// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Reads the CSS declarations of a rich text <c>style</c> attribute or a default style (<c>/DS</c>), the properties of
/// ISO 32000-2 12.7.4.3 that change how text looks. Properties it does not know are ignored.
/// </summary>
internal static class RichStyleReader
{
    /// <summary>The length of a 4-character lookup name.</summary>
    private const int NameLength4 = 4;

    /// <summary>The length of a 5-character lookup name.</summary>
    private const int NameLength5 = 5;

    /// <summary>The length of a 9-character lookup name.</summary>
    private const int NameLength9 = 9;

    /// <summary>The length of a 10-character lookup name.</summary>
    private const int NameLength10 = 10;

    /// <summary>The length of a 11-character lookup name.</summary>
    private const int NameLength11 = 11;

    /// <summary>The length of a 12-character lookup name.</summary>
    private const int NameLength12 = 12;

    /// <summary>The length of a 15-character lookup name.</summary>
    private const int NameLength15 = 15;

    /// <summary>The length of a 3-character lookup name.</summary>
    private const int NameLength3 = 3;

    /// <summary>The length of a 6-character lookup name.</summary>
    private const int NameLength6 = 6;

    /// <summary>The length of a 7-character lookup name.</summary>
    private const int NameLength7 = 7;

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

    /// <summary>Applies a list of CSS declarations to a style.</summary>
    /// <param name="css">The declarations, such as <c>font-size:12pt; color:#FF0000</c>; may be empty.</param>
    /// <param name="style">The style to start from.</param>
    /// <returns>The style with the declarations applied.</returns>
    internal static RichStyle Apply(ReadOnlySpan<char> css, RichStyle style)
    {
        var result = style;
        foreach (var range in css.Split(';'))
        {
            var declaration = css[range];
            var colon = declaration.IndexOf(':');
            if (colon > 0)
            {
                result = ApplyDeclaration(declaration[..colon].Trim(), declaration[(colon + 1)..].Trim(), result);
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

        if (value.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase) && value.EndsWith(")", StringComparison.Ordinal))
        {
            return TryReadFunctionalColor(value[FunctionPrefixLength..^1], out color);
        }

        var named = FindColor(value);
        if (named < 0)
        {
            return false;
        }

        color = named;
        return true;
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

    /// <summary>Applies one CSS declaration.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <param name="value">The CSS value.</param>
    /// <param name="style">The inherited style.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static RichStyle ApplyDeclaration(ReadOnlySpan<char> name, ReadOnlySpan<char> value, RichStyle style) => name.Length switch
    {
        NameLength4 => ApplyDeclarationLength4(name, value, style),
        NameLength5 => ApplyDeclarationLength5(name, value, style),
        NameLength9 => ApplyDeclarationLength9(name, value, style),
        NameLength10 => ApplyDeclarationLength10(name, value, style),
        NameLength11 => ApplyDeclarationLength11(name, value, style),
        NameLength12 => ApplyDeclarationLength12(name, value, style),
        NameLength15 => ApplyDeclarationLength15(name, value, style),
        _ => style,
    };

    /// <summary>Applies one CSS declaration.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <param name="value">The CSS value.</param>
    /// <param name="style">The inherited style.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static RichStyle ApplyDeclarationLength4(ReadOnlySpan<char> name, ReadOnlySpan<char> value, RichStyle style) => name switch
    {
        var key when key.Equals("font", StringComparison.OrdinalIgnoreCase) => ReadShorthand(style, value),
        _ => style,
    };

    /// <summary>Applies one CSS declaration.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <param name="value">The CSS value.</param>
    /// <param name="style">The inherited style.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static RichStyle ApplyDeclarationLength5(ReadOnlySpan<char> name, ReadOnlySpan<char> value, RichStyle style) => name switch
    {
        var key when key.Equals("color", StringComparison.OrdinalIgnoreCase) => TryReadColor(value, out var color) ? style with { Color = color } : style,
        _ => style,
    };

    /// <summary>Applies one CSS declaration.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <param name="value">The CSS value.</param>
    /// <param name="style">The inherited style.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static RichStyle ApplyDeclarationLength9(ReadOnlySpan<char> name, ReadOnlySpan<char> value, RichStyle style) => name switch
    {
        var key when key.Equals("font-size", StringComparison.OrdinalIgnoreCase) => TryReadSize(value, out var size) ? style with { Size = size } : style,
        _ => style,
    };

    /// <summary>Applies one CSS declaration.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <param name="value">The CSS value.</param>
    /// <param name="style">The inherited style.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static RichStyle ApplyDeclarationLength10(ReadOnlySpan<char> name, ReadOnlySpan<char> value, RichStyle style) => name switch
    {
        var key when key.Equals("font-style", StringComparison.OrdinalIgnoreCase) => style with { Italic = IsItalic(value) },
        var key when key.Equals("text-align", StringComparison.OrdinalIgnoreCase) => style with { Alignment = ReadAlignment(value, style.Alignment) },
        _ => style,
    };

    /// <summary>Applies one CSS declaration.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <param name="value">The CSS value.</param>
    /// <param name="style">The inherited style.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static RichStyle ApplyDeclarationLength11(ReadOnlySpan<char> name, ReadOnlySpan<char> value, RichStyle style) => name switch
    {
        var key when key.Equals("font-family", StringComparison.OrdinalIgnoreCase) => style with { Family = ReadFamily(value) },
        var key when key.Equals("font-weight", StringComparison.OrdinalIgnoreCase) => style with { Bold = IsBold(value) },
        _ => style,
    };

    /// <summary>Applies one CSS declaration.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <param name="value">The CSS value.</param>
    /// <param name="style">The inherited style.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static RichStyle ApplyDeclarationLength12(ReadOnlySpan<char> name, ReadOnlySpan<char> value, RichStyle style) => name switch
    {
        var key when key.Equals("xfa-spacerun", StringComparison.OrdinalIgnoreCase) => style with { KeepSpaces = value.Equals("yes", StringComparison.OrdinalIgnoreCase) },
        _ => style,
    };

    /// <summary>Applies one CSS declaration.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <param name="value">The CSS value.</param>
    /// <param name="style">The inherited style.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static RichStyle ApplyDeclarationLength15(ReadOnlySpan<char> name, ReadOnlySpan<char> value, RichStyle style) => name switch
    {
        var key when key.Equals("text-decoration", StringComparison.OrdinalIgnoreCase) => ReadDecoration(style, value),
        _ => style,
    };

    /// <summary>Finds a named CSS colour.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static int FindColor(ReadOnlySpan<char> name) => name.Length switch
    {
        NameLength3 => FindColorLength3(name),
        NameLength4 => FindColorLength4(name),
        NameLength5 => FindColorLength5(name),
        NameLength6 => FindColorLength6(name),
        NameLength7 => FindColorLength7(name),
        _ => -1,
    };

    /// <summary>Finds a named CSS colour.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static int FindColorLength3(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("red", StringComparison.OrdinalIgnoreCase) => 0xFF0000,
        _ => -1,
    };

    /// <summary>Finds a named CSS colour.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static int FindColorLength4(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("blue", StringComparison.OrdinalIgnoreCase) => 0x0000FF,
        var key when key.Equals("gray", StringComparison.OrdinalIgnoreCase) => 0x808080,
        var key when key.Equals("grey", StringComparison.OrdinalIgnoreCase) => 0x808080,
        var key when key.Equals("navy", StringComparison.OrdinalIgnoreCase) => 0x000080,
        var key when key.Equals("teal", StringComparison.OrdinalIgnoreCase) => 0x008080,
        var key when key.Equals("lime", StringComparison.OrdinalIgnoreCase) => 0x00FF00,
        var key when key.Equals("aqua", StringComparison.OrdinalIgnoreCase) => 0x00FFFF,
        _ => -1,
    };

    /// <summary>Finds a named CSS colour.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static int FindColorLength5(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("black", StringComparison.OrdinalIgnoreCase) => 0x000000,
        var key when key.Equals("white", StringComparison.OrdinalIgnoreCase) => 0xFFFFFF,
        var key when key.Equals("green", StringComparison.OrdinalIgnoreCase) => 0x008000,
        var key when key.Equals("olive", StringComparison.OrdinalIgnoreCase) => 0x808000,
        _ => -1,
    };

    /// <summary>Finds a named CSS colour.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static int FindColorLength6(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("yellow", StringComparison.OrdinalIgnoreCase) => 0xFFFF00,
        var key when key.Equals("silver", StringComparison.OrdinalIgnoreCase) => 0xC0C0C0,
        var key when key.Equals("maroon", StringComparison.OrdinalIgnoreCase) => 0x800000,
        var key when key.Equals("purple", StringComparison.OrdinalIgnoreCase) => 0x800080,
        var key when key.Equals("orange", StringComparison.OrdinalIgnoreCase) => 0xFFA500,
        _ => -1,
    };

    /// <summary>Finds a named CSS colour.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static int FindColorLength7(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("fuchsia", StringComparison.OrdinalIgnoreCase) => 0xFF00FF,
        _ => -1,
    };
}
