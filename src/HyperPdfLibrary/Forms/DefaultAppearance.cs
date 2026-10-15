// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// A field's default appearance string (<c>/DA</c>): the font, its size and the text colour. It reads the string's bytes in
/// place, so parsing allocates nothing for a font name without escapes.
/// </summary>
[DebuggerDisplay("DefaultAppearance: {FontSize} {Color}")]
internal readonly ref struct DefaultAppearance
{
    /// <summary>The operand count of the gray operator.</summary>
    private const int GrayOperands = 1;

    /// <summary>The operand count of the RGB operator.</summary>
    private const int RgbOperands = 3;

    /// <summary>The operand count of the CMYK operator.</summary>
    private const int CmykOperands = 4;

    /// <summary>The operand count of the font operator.</summary>
    private const int FontOperands = 2;

    /// <summary>The number of hex digits in a name escape.</summary>
    private const int HexLength = 2;

    /// <summary>The place of the third operand.</summary>
    private const int ThirdOperand = 2;

    /// <summary>The place of the fourth operand.</summary>
    private const int FourthOperand = 3;

    /// <summary>The most recent tokens kept: the longest operator's operands and the operator.</summary>
    private const int Window = CmykOperands + 1;

    /// <summary>The slots of a token's start and length in the window.</summary>
    private const int SlotsPerToken = 2;

    /// <summary>Initializes a new instance of the <see cref="DefaultAppearance"/> struct.</summary>
    /// <param name="fontName">The font's name in the resources, without the slash and with escapes decoded.</param>
    /// <param name="fontSize">The size; 0 means the text is fitted to the field.</param>
    /// <param name="color">The text colour.</param>
    internal DefaultAppearance(ReadOnlySpan<byte> fontName, float fontSize, DaColor color)
    {
        FontName = fontName;
        FontSize = fontSize;
        Color = color;
    }

    /// <summary>Gets the appearance used when a field has none.</summary>
    internal static DefaultAppearance Empty => default;

    /// <summary>Gets the font's name in the resources, without the slash; empty when the string names none.</summary>
    internal ReadOnlySpan<byte> FontName { get; }

    /// <summary>Gets the size; 0 means the text is fitted to the field.</summary>
    internal float FontSize { get; }

    /// <summary>Gets the text colour.</summary>
    internal DaColor Color { get; }

    /// <summary>Finds the default appearance of a widget: its own, its field's, then the form's.</summary>
    /// <param name="widget">The widget dictionary.</param>
    /// <param name="field">The field dictionary.</param>
    /// <param name="form">The AcroForm dictionary, or <see langword="null"/>.</param>
    /// <returns>The parsed appearance.</returns>
    internal static DefaultAppearance Find(PdfDictionary widget, PdfDictionary field, PdfDictionary? form)
    {
        if (widget.ContainsKey(KnownName.DA))
        {
            return Parse(Bytes(widget.Get(KnownName.DA)));
        }

        var inherited = FieldAttributes.Find(field, KnownName.DA);
        if (!inherited.IsNull)
        {
            return Parse(Bytes(inherited));
        }

        return form is null ? Empty : Parse(Bytes(form.Get(KnownName.DA)));
    }

    /// <summary>Parses a default appearance string.</summary>
    /// <param name="text">The string's bytes.</param>
    /// <returns>The parsed appearance, which refers to <paramref name="text"/> for the font's name.</returns>
    internal static DefaultAppearance Parse(ReadOnlySpan<byte> text)
    {
        Span<int> ring = stackalloc int[Window * SlotsPerToken];
        var seen = 0;
        var name = default(ReadOnlySpan<byte>);
        var size = 0F;
        var color = default(DaColor);
        var lexer = new PdfLexer(text);
        PdfTokenKind kind;
        while ((kind = lexer.Next()) != PdfTokenKind.EndOfData)
        {
            var slot = (seen % Window) * SlotsPerToken;
            ring[slot] = lexer.LexemeStart;
            ring[slot + 1] = lexer.LexemeLength;
            seen++;
            switch (kind == PdfTokenKind.Keyword ? Operator(lexer.Lexeme) : DaOperator.None)
            {
                case DaOperator.Font when seen > FontOperands:
                {
                    name = DecodeName(Token(text, ring, seen, FontOperands));
                    size = ParseNumber(Token(text, ring, seen, 1));
                    break;
                }

                case DaOperator.Gray:
                {
                    color = ReadColor(text, ring, seen, DaColorKind.Gray, GrayOperands);
                    break;
                }

                case DaOperator.Rgb:
                {
                    color = ReadColor(text, ring, seen, DaColorKind.Rgb, RgbOperands);
                    break;
                }

                case DaOperator.Cmyk:
                {
                    color = ReadColor(text, ring, seen, DaColorKind.Cmyk, CmykOperands);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        return new(name, size, color);
    }

    /// <summary>Reads the font name a default appearance string names.</summary>
    /// <param name="appearance">The string.</param>
    /// <returns>The font's name; empty when the string names none.</returns>
    internal static string ReadFontName(string appearance)
    {
        var bytes = Encoding.Latin1.GetBytes(appearance);
        return Encoding.Latin1.GetString(Parse(bytes).FontName);
    }

    /// <summary>Reads the font size a default appearance string sets.</summary>
    /// <param name="appearance">The string.</param>
    /// <returns>The size; 0 means the text is fitted to the field.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float ReadFontSize(string appearance) => Parse(Encoding.Latin1.GetBytes(appearance)).FontSize;

    /// <summary>Reads a value as the bytes of a string.</summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>The bytes; empty when the value is not a string.</returns>
    private static ReadOnlySpan<byte> Bytes(PdfValue value) => value.Kind == PdfKind.String ? value.AsStringBytes() : default;

    /// <summary>Works out which operator a token is.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The operator; none when it is not one the appearance reads.</returns>
    private static DaOperator Operator(ReadOnlySpan<byte> token) => token switch
    {
        [(byte)'T', (byte)'f'] => DaOperator.Font,
        [(byte)'g'] => DaOperator.Gray,
        [(byte)'r', (byte)'g'] => DaOperator.Rgb,
        [(byte)'k'] => DaOperator.Cmyk,
        _ => DaOperator.None,
    };

    /// <summary>Gets an earlier token.</summary>
    /// <param name="text">The string.</param>
    /// <param name="ring">The starts and lengths of the latest tokens.</param>
    /// <param name="seen">The tokens seen so far, including the operator.</param>
    /// <param name="back">How many tokens before the operator.</param>
    /// <returns>The token.</returns>
    private static ReadOnlySpan<byte> Token(ReadOnlySpan<byte> text, scoped ReadOnlySpan<int> ring, int seen, int back)
    {
        var slot = ((seen - 1 - back) % Window) * SlotsPerToken;
        return text.Slice(ring[slot], ring[slot + 1]);
    }

    /// <summary>Reads a colour from the operands before its operator.</summary>
    /// <param name="text">The string.</param>
    /// <param name="ring">The starts and lengths of the latest tokens.</param>
    /// <param name="seen">The tokens seen so far, including the operator.</param>
    /// <param name="kind">The colour operator.</param>
    /// <param name="operands">How many operands it takes.</param>
    /// <returns>The colour; black when operands are missing.</returns>
    private static DaColor ReadColor(ReadOnlySpan<byte> text, scoped ReadOnlySpan<int> ring, int seen, DaColorKind kind, int operands)
    {
        if (seen - 1 < operands)
        {
            return default;
        }

        Span<float> values = stackalloc float[CmykOperands];
        values.Clear();
        for (var i = 0; i < operands; i++)
        {
            values[i] = ParseNumber(Token(text, ring, seen, operands - i));
        }

        return new(kind, values[0], values[1], values[ThirdOperand], values[FourthOperand]);
    }

    /// <summary>Removes the slash of a name token and decodes its escapes.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The name; the token's own bytes unless it holds an escape.</returns>
    private static ReadOnlySpan<byte> DecodeName(ReadOnlySpan<byte> token)
    {
        var name = !token.IsEmpty && token[0] == (byte)'/' ? token[1..] : token;
        if (!name.Contains((byte)'#'))
        {
            return name;
        }

        // The result outlives this parse, so escaped names need owned bytes.
        var decoded = new byte[name.Length];
        var written = 0;
        for (var i = 0; i < name.Length; i++)
        {
            if (name[i] == (byte)'#' && i + HexLength < name.Length && TryReadHex(name.Slice(i + 1, HexLength), out var code))
            {
                decoded[written] = code;
                i += HexLength;
            }
            else
            {
                decoded[written] = name[i];
            }

            written++;
        }

        return decoded.AsSpan(0, written);
    }

    /// <summary>Reads two hex digits.</summary>
    /// <param name="digits">The digits.</param>
    /// <param name="value">Receives the byte.</param>
    /// <returns><see langword="true"/> when both are hex digits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryReadHex(ReadOnlySpan<byte> digits, out byte value) =>
        byte.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);

    /// <summary>Parses a number, treating anything else as zero.</summary>
    /// <param name="token">The token.</param>
    /// <returns>The number.</returns>
    private static float ParseNumber(ReadOnlySpan<byte> token) =>
        float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && float.IsFinite(value) ? value : 0;
}
