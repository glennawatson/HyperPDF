// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Reads and writes the entries of annotation dictionaries (PDF 32000-2, 12.5): rectangles, colours, borders, flags,
/// text, dates, point arrays and appearances. Writers change the dictionary they are given, so callers copy a parsed
/// dictionary with <see cref="PdfDictionary.Clone"/> first.
/// </summary>
public static partial class PdfAnnotations
{
    /// <summary>The entries a new annotation is sized for, so filling it in rarely grows it.</summary>
    private const int InitialEntries = 12;

    /// <summary>The largest colour channel value.</summary>
    private const double ChannelMax = 255;

    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>One channel's bits.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The components of an RGB colour.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK colour.</summary>
    private const int CmykComponents = 4;

    /// <summary>The index of the black component of a CMYK colour.</summary>
    private const int BlackComponent = 3;

    /// <summary>The index of the third component of a colour.</summary>
    private const int ThirdComponent = 2;

    /// <summary>The scale of the sixth decimal, at which colour components are rounded up.</summary>
    private const double SixDecimals = 1_000_000;

    /// <summary>The entries of a <c>/Border</c> array: two corner radii and the width.</summary>
    private const int BorderEntries = 3;

    /// <summary>The index of the width in a <c>/Border</c> array.</summary>
    private const int BorderWidthIndex = 2;

    /// <summary>The bytes a formatted date can take; at least <see cref="PdfDate.MaxFormattedLength"/>.</summary>
    private const int DateBytes = 32;

    /// <summary>The characters a number stored as text can take.</summary>
    private const int NumberChars = 32;

    /// <summary>The size of a UTF-16 code unit in bytes.</summary>
    private const int Utf16Size = 2;

    /// <summary>The format of numbers stored as text: up to three decimals, enough for a point value at screen precision.</summary>
    private const string NumberTextFormat = "0.###";

    /// <summary>Gets the UTF-16 big-endian byte order mark.</summary>
    private static ReadOnlySpan<byte> Utf16Mark => [0xFE, 0xFF];

    /// <summary>Gets the UTF-8 byte order mark, which PDF 2.0 text strings may carry.</summary>
    private static ReadOnlySpan<byte> Utf8Mark => [0xEF, 0xBB, 0xBF];

    /// <summary>Creates an annotation dictionary with its type, subtype and rectangle.</summary>
    /// <param name="owner">The document the annotation belongs to.</param>
    /// <param name="subtype">The subtype, such as <see cref="KnownName.Ink"/>.</param>
    /// <param name="rectangle">The rectangle in user space.</param>
    /// <returns>The dictionary.</returns>
    public static PdfDictionary Create(PdfObjectStore? owner, PdfName subtype, PdfRectangle rectangle)
    {
        var dictionary = new PdfDictionary(owner, InitialEntries);
        dictionary.Set(KnownName.Type, PdfValue.FromName(KnownName.Annot));
        dictionary.Set(KnownName.Subtype, PdfValue.FromName(subtype));
        SetRectangle(dictionary, rectangle);
        return dictionary;
    }

    /// <summary>Sets <c>/Rect</c>.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="rectangle">The rectangle in user space.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static void SetRectangle(PdfDictionary annotation, PdfRectangle rectangle)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        annotation.Set(KnownName.Rect, PdfValue.FromArray(rectangle.ToArray(annotation.Owner)));
    }

    /// <summary>Gets <c>/Rect</c>, normalised; a missing or malformed rectangle reads as empty.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The rectangle.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static PdfRectangle GetRectangle(PdfDictionary annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return annotation.TryGetRectangle(KnownName.Rect, out var rectangle) ? rectangle : default;
    }

    /// <summary>
    /// Sets an RGB colour entry such as <c>/C</c> or <c>/IC</c>. Each component is rounded up at the sixth decimal, so
    /// readers that scale it back to a byte and truncate get the same byte.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static void SetColor(PdfDictionary annotation, PdfName key, uint color)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var array = new PdfArray(annotation.Owner, RgbComponents);
        array.Add(Component(color >> RedShift));
        array.Add(Component(color >> GreenShift));
        array.Add(Component(color));
        annotation.Set(key, PdfValue.FromArray(array));
    }

    /// <summary>Reads a colour entry: gray, RGB or CMYK, converted to RGB; an empty array is transparent.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns><see langword="true"/> when the entry holds a colour.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static bool TryGetColor(PdfDictionary annotation, PdfName key, out uint color)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        color = 0;
        if (annotation.GetArray(key) is not { } array)
        {
            return false;
        }

        Span<float> values = stackalloc float[CmykComponents];
        var count = array.ReadNumbers(values);
        switch (count)
        {
            case 1:
            {
                color = Pack(values[0], values[0], values[0]);
                return true;
            }

            case RgbComponents:
            {
                color = Pack(values[0], values[1], values[ThirdComponent]);
                return true;
            }

            case CmykComponents:
            {
                var white = 1 - values[BlackComponent];
                color = Pack((1 - values[0]) * white, (1 - values[1]) * white, (1 - values[ThirdComponent]) * white);
                return true;
            }

            default:
            {
                return count == 0;
            }
        }
    }

    /// <summary>Sets the constant opacity, <c>/CA</c>.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="opacity">The opacity from 0 to 1.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static void SetOpacity(PdfDictionary annotation, float opacity)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        annotation.Set(KnownName.CA, PdfNumber.ToValue(Math.Clamp(opacity, 0, 1)));
    }

    /// <summary>Sets <c>/Border</c> to square corners and a width (ISO 32000-2 Table 168 /Border: horizontal radius 0, vertical radius 0, width).</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="width">The line width in points.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static void SetBorderWidth(PdfDictionary annotation, float width)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var border = new PdfArray(annotation.Owner, BorderEntries);
        border.Add(PdfValue.FromInteger(0));
        border.Add(PdfValue.FromInteger(0));
        border.Add(PdfNumber.ToValue(width));
        annotation.Set(KnownName.Border, PdfValue.FromArray(border));
    }

    /// <summary>Gets the width from <c>/Border</c>.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The width, or zero when there is no complete <c>/Border</c> array.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static float GetBorderWidth(PdfDictionary annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return annotation.GetArray(KnownName.Border) is { Count: >= BorderEntries } border ? border.GetSingle(BorderWidthIndex) : 0;
    }

    /// <summary>Sets <c>/F</c>.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="flags">The flags.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static void SetFlags(PdfDictionary annotation, PdfAnnotationFlags flags)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        annotation.Set(KnownName.F, PdfValue.FromInteger((int)flags));
    }

    /// <summary>Gets <c>/F</c>.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The flags, or none when missing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static PdfAnnotationFlags GetFlags(PdfDictionary annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return (PdfAnnotationFlags)annotation.GetInt32(KnownName.F);
    }

    /// <summary>Sets a text string entry, keeping the encoding of the entry it replaces; a new entry is PDFDocEncoding when it fits, UTF-8 in a PDF 2.0 document, and UTF-16 otherwise.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <param name="text">The text.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static void SetText(PdfDictionary annotation, PdfName key, string text)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        ArgumentNullException.ThrowIfNull(text);
        annotation.Set(key, PdfValue.FromString(PdfText.Encode(text, annotation.GetStringBytes(key), annotation.Owner?.Version)));
    }

    /// <summary>Gets a text string entry.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <returns>The text, or an empty string when missing or not a string.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static string GetText(PdfDictionary annotation, PdfName key)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return annotation.GetText(key) ?? string.Empty;
    }

    /// <summary>Determines whether a text string or name entry holds some text.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the entry is a non-empty string, a name, or a number.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static bool HasText(PdfDictionary annotation, PdfName key)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var value = annotation.Get(key);
        return value.Kind switch
        {
            PdfKind.String => IsNonEmptyText(value.AsStringBytes()),
            PdfKind.Name or PdfKind.Integer or PdfKind.Real => true,
            _ => false,
        };
    }

    /// <summary>
    /// Determines whether a text string or name entry equals an ASCII value, decoding PDFDocEncoding and UTF-16 without
    /// allocating.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <param name="ascii">The expected ASCII text.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static bool TextEquals(PdfDictionary annotation, PdfName key, ReadOnlySpan<byte> ascii)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var value = annotation.Get(key);
        return value.Kind == PdfKind.Name
            ? annotation.Owner is { } owner && owner.Names.NameEquals(value.AsName(), ascii)
            : value.Kind == PdfKind.String && TextBytesEqual(value.AsStringBytes(), ascii);
    }

    /// <summary>Sets a date entry such as <c>/M</c>.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <param name="date">The date.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static void SetDate(PdfDictionary annotation, PdfName key, DateTimeOffset date)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        Span<byte> buffer = stackalloc byte[DateBytes];
        var length = PdfDate.Format(date, buffer);
        annotation.Set(key, PdfValue.FromString(buffer[..length].ToArray()));
    }

    /// <summary>Gets a date entry.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <returns>The date, or <see langword="null"/> when missing or unreadable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static DateTimeOffset? GetDate(PdfDictionary annotation, PdfName key)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var bytes = annotation.GetStringBytes(key);
        return bytes.IsEmpty ? null : PdfDate.Parse(bytes);
    }

    /// <summary>Stores a number as a text string with up to three decimals, so a viewer's own numeric keys survive in any reader.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <param name="number">The number.</param>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static void SetNumberText(PdfDictionary annotation, PdfName key, float number)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        Span<byte> buffer = stackalloc byte[NumberChars];
        _ = number.TryFormat(buffer, out var written, NumberTextFormat, CultureInfo.InvariantCulture);
        annotation.Set(key, PdfValue.FromString(buffer[..written].ToArray()));
    }

    /// <summary>Reads a number stored as a number or as text.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The key.</param>
    /// <returns>The number, or zero when missing or unreadable.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="annotation"/> is <see langword="null"/>.</exception>
    public static float GetNumberText(PdfDictionary annotation, PdfName key)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var value = annotation.Get(key);
        if (value.IsNumber)
        {
            return value.AsSingle();
        }

        Span<char> chars = stackalloc char[NumberChars];
        var length = DecodeAscii(value.AsStringBytes(), chars);
        return float.TryParse(chars[..length], NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }

    /// <summary>Makes one colour component, rounded up at the sixth decimal.</summary>
    /// <param name="channel">The channel in its low byte.</param>
    /// <returns>The component.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfValue Component(uint channel) =>
        PdfNumber.ToValue(Math.Min(1, Math.Ceiling((channel & ChannelMask) / ChannelMax * SixDecimals) / SixDecimals));

    /// <summary>Packs three components from 0 to 1 into 0xRRGGBB, rounding each to the nearest byte.</summary>
    /// <param name="red">The red component.</param>
    /// <param name="green">The green component.</param>
    /// <param name="blue">The blue component.</param>
    /// <returns>The colour.</returns>
    private static uint Pack(float red, float green, float blue) =>
        (Channel(red) << RedShift) | (Channel(green) << GreenShift) | Channel(blue);

    /// <summary>Converts a component from 0 to 1 to a byte.</summary>
    /// <param name="component">The component.</param>
    /// <returns>The byte value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Channel(float component) => (uint)Math.Clamp(Math.Round(component * ChannelMax), 0, ChannelMax);

    /// <summary>Determines whether a text string's bytes hold at least one character.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns><see langword="true"/> when not empty, a bare byte order mark aside.</returns>
    private static bool IsNonEmptyText(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Utf8Mark))
        {
            return bytes.Length > Utf8Mark.Length;
        }

        return bytes.StartsWith(Utf16Mark) ? bytes.Length > Utf16Mark.Length : !bytes.IsEmpty;
    }

    /// <summary>Compares a text string's bytes with ASCII text.</summary>
    /// <param name="bytes">The string's bytes.</param>
    /// <param name="ascii">The ASCII text.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    private static bool TextBytesEqual(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> ascii)
    {
        if (bytes.StartsWith(Utf8Mark))
        {
            return bytes[Utf8Mark.Length..].SequenceEqual(ascii);
        }

        if (!bytes.StartsWith(Utf16Mark))
        {
            return bytes.SequenceEqual(ascii);
        }

        var units = bytes[Utf16Mark.Length..];
        if (units.Length != ascii.Length * Utf16Size)
        {
            return false;
        }

        for (var i = 0; i < ascii.Length; i++)
        {
            if (BinaryPrimitives.ReadUInt16BigEndian(units[(i * Utf16Size)..]) != ascii[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Decodes the ASCII characters of a short text string, PDFDocEncoding or UTF-16.</summary>
    /// <param name="bytes">The string's bytes.</param>
    /// <param name="destination">The characters.</param>
    /// <returns>The number of characters written.</returns>
    private static int DecodeAscii(ReadOnlySpan<byte> bytes, Span<char> destination)
    {
        bytes = bytes.StartsWith(Utf8Mark) ? bytes[Utf8Mark.Length..] : bytes;
        var utf16 = bytes.StartsWith(Utf16Mark);
        var source = utf16 ? bytes[Utf16Mark.Length..] : bytes;
        var step = utf16 ? Utf16Size : 1;
        var count = 0;
        for (var at = utf16 ? 1 : 0; at < source.Length && count < destination.Length; at += step)
        {
            destination[count] = (char)source[at];
            count++;
        }

        return count;
    }
}
