// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Works out how many bytes an unfiltered inline image holds from its dictionary. Colour spaces named through the page
/// resources are unknown here, so only device, calibrated, Indexed and mask images have a computed size.
/// </summary>
internal static class InlineImageSize
{
    /// <summary>The bits in a byte, rounding a row up to whole bytes.</summary>
    private const int ByteBits = 8;

    /// <summary>The components of an RGB colour space.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK colour space.</summary>
    private const int CmykComponents = 4;

    /// <summary>The largest size computed, which bounds overflow.</summary>
    private const long MaxLength = 1L << 31;

    /// <summary>The dictionary keys that decide the size.</summary>
    private enum InlineKey
    {
        /// <summary>A key that does not affect the size.</summary>
        Other = 0,

        /// <summary>The width.</summary>
        Width = 1,

        /// <summary>The height.</summary>
        Height = 2,

        /// <summary>The bits per component.</summary>
        Bits = 3,

        /// <summary>The colour space.</summary>
        ColorSpace = 4,

        /// <summary>The image mask flag.</summary>
        Mask = 5,

        /// <summary>The filter.</summary>
        Filter = 6,
    }

    /// <summary>Computes the data length of an unfiltered inline image.</summary>
    /// <param name="dictionary">The dictionary entries between <c>BI</c> and <c>ID</c>.</param>
    /// <param name="length">The data length in bytes.</param>
    /// <returns><see langword="true"/> when the length could be computed.</returns>
    internal static bool TryGetDataLength(ReadOnlySpan<byte> dictionary, out long length)
    {
        length = 0;
        var fields = ReadFields(dictionary);

        if (fields.Filtered || fields.Width <= 0 || fields.Height <= 0)
        {
            return false;
        }

        var bits = fields.Mask ? 1 : fields.Bits;
        var components = fields.Mask ? 1 : fields.Components;
        if (bits <= 0 || components <= 0)
        {
            return false;
        }

        var rowBytes = ((fields.Width * components * bits) + ByteBits - 1) / ByteBits;
        length = rowBytes * fields.Height;
        return length <= MaxLength;
    }

    /// <summary>Reads the size-related entries; an abbreviated key beats the full key for the same entry (ISO 32000-2 §8.9.7), so the full one is read and dropped.</summary>
    /// <param name="dictionary">The dictionary entries between <c>BI</c> and <c>ID</c>.</param>
    /// <returns>The fields.</returns>
    private static Fields ReadFields(ReadOnlySpan<byte> dictionary)
    {
        var fields = default(Fields);
        var overridden = Abbreviated(dictionary);
        var lexer = new PdfLexer(dictionary);
        var scratch = default(Fields);
        while (lexer.Next() == PdfTokenKind.Name)
        {
            var key = Classify(lexer.Lexeme);
            var ignored = key != InlineKey.Other && !IsAbbreviation(lexer.Lexeme) && (overridden & (1 << (int)key)) != 0;
            ReadValue(ref lexer, key, ref ignored ? ref scratch : ref fields);
        }

        return fields;
    }

    /// <summary>Finds which of the size-related entries are written with their abbreviated key.</summary>
    /// <param name="dictionary">The dictionary entries between <c>BI</c> and <c>ID</c>.</param>
    /// <returns>A bit for each <see cref="InlineKey"/> that has an abbreviated key.</returns>
    private static int Abbreviated(ReadOnlySpan<byte> dictionary)
    {
        var found = 0;
        var lexer = new PdfLexer(dictionary);
        var scratch = default(Fields);
        while (lexer.Next() == PdfTokenKind.Name)
        {
            var key = Classify(lexer.Lexeme);
            found |= key != InlineKey.Other && IsAbbreviation(lexer.Lexeme) ? 1 << (int)key : 0;
            ReadValue(ref lexer, key, ref scratch);
        }

        return found;
    }

    /// <summary>Determines whether a key is one of the inline abbreviations rather than a full name.</summary>
    /// <param name="name">The key's bytes.</param>
    /// <returns><see langword="true"/> for an abbreviation.</returns>
    private static bool IsAbbreviation(ReadOnlySpan<byte> name) =>
        name.SequenceEqual("W"u8) || name.SequenceEqual("H"u8) || name.SequenceEqual("BPC"u8) || name.SequenceEqual("CS"u8) || name.SequenceEqual("IM"u8) || name.SequenceEqual("F"u8);

    /// <summary>Identifies a dictionary key, expanding the inline abbreviations.</summary>
    /// <param name="name">The key's bytes.</param>
    /// <returns>The key.</returns>
    private static InlineKey Classify(ReadOnlySpan<byte> name)
    {
        if (Matches(name, "W"u8, "Width"u8))
        {
            return InlineKey.Width;
        }

        if (Matches(name, "H"u8, "Height"u8))
        {
            return InlineKey.Height;
        }

        if (Matches(name, "BPC"u8, "BitsPerComponent"u8))
        {
            return InlineKey.Bits;
        }

        if (Matches(name, "CS"u8, "ColorSpace"u8))
        {
            return InlineKey.ColorSpace;
        }

        if (Matches(name, "IM"u8, "ImageMask"u8))
        {
            return InlineKey.Mask;
        }

        return Matches(name, "F"u8, "Filter"u8) ? InlineKey.Filter : InlineKey.Other;
    }

    /// <summary>Reads a key's value into the fields, skipping arrays and dictionaries.</summary>
    /// <param name="lexer">The lexer, after the key.</param>
    /// <param name="key">The key.</param>
    /// <param name="fields">The fields.</param>
    private static void ReadValue(ref PdfLexer lexer, InlineKey key, ref Fields fields)
    {
        var kind = lexer.Next();
        if (key == InlineKey.Filter)
        {
            fields.Filtered = true;
        }

        switch (kind)
        {
            case PdfTokenKind.Number:
            {
                if (PdfNumber.TryParse(lexer.Lexeme, out var number))
                {
                    fields.SetNumber(key, number.AsInteger());
                }

                break;
            }

            case PdfTokenKind.Name:
            {
                if (key == InlineKey.ColorSpace)
                {
                    fields.Components = ComponentsOf(lexer.Lexeme);
                }

                break;
            }

            case PdfTokenKind.Keyword:
            {
                if (key == InlineKey.Mask)
                {
                    fields.Mask = lexer.Lexeme.SequenceEqual("true"u8);
                }

                break;
            }

            case PdfTokenKind.ArrayStart:
            {
                ReadArray(ref lexer, key, ref fields);
                break;
            }

            case PdfTokenKind.DictionaryStart:
            {
                SkipComposite(ref lexer);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Reads an array value; an Indexed colour space array has one component.</summary>
    /// <param name="lexer">The lexer, after the opening bracket.</param>
    /// <param name="key">The key.</param>
    /// <param name="fields">The fields.</param>
    private static void ReadArray(ref PdfLexer lexer, InlineKey key, ref Fields fields)
    {
        if (key == InlineKey.ColorSpace && lexer.Next() == PdfTokenKind.Name)
        {
            fields.Components = ComponentsOf(lexer.Lexeme);
        }

        SkipComposite(ref lexer);
    }

    /// <summary>Skips to the end of an array or dictionary whose opening token was just read.</summary>
    /// <param name="lexer">The lexer.</param>
    private static void SkipComposite(ref PdfLexer lexer)
    {
        var depth = 1;
        while (depth > 0)
        {
            var kind = lexer.Next();
            if (kind == PdfTokenKind.EndOfData)
            {
                return;
            }

            depth += kind is PdfTokenKind.ArrayStart or PdfTokenKind.DictionaryStart ? 1 : 0;
            depth -= kind is PdfTokenKind.ArrayEnd or PdfTokenKind.DictionaryEnd ? 1 : 0;
        }
    }

    /// <summary>Gets the components of a colour space name.</summary>
    /// <param name="name">The name's bytes.</param>
    /// <returns>The components, or zero when the name needs the page resources.</returns>
    private static int ComponentsOf(ReadOnlySpan<byte> name)
    {
        if (Matches(name, "G"u8, "DeviceGray"u8) || name.SequenceEqual("CalGray"u8) || Matches(name, "I"u8, "Indexed"u8))
        {
            return 1;
        }

        if (Matches(name, "RGB"u8, "DeviceRGB"u8) || name.SequenceEqual("CalRGB"u8))
        {
            return RgbComponents;
        }

        return Matches(name, "CMYK"u8, "DeviceCMYK"u8) ? CmykComponents : 0;
    }

    /// <summary>Determines whether a name is either of two spellings.</summary>
    /// <param name="name">The name's bytes.</param>
    /// <param name="abbreviation">The inline abbreviation.</param>
    /// <param name="full">The full name.</param>
    /// <returns><see langword="true"/> when it is one of them.</returns>
    private static bool Matches(ReadOnlySpan<byte> name, ReadOnlySpan<byte> abbreviation, ReadOnlySpan<byte> full) =>
        name.SequenceEqual(abbreviation) || name.SequenceEqual(full);

    /// <summary>The size-related values read so far.</summary>
    private struct Fields
    {
        /// <summary>The most bits per component an image has.</summary>
        private const int MaxBits = 16;

        /// <summary>Gets or sets the width in pixels.</summary>
        public long Width { get; set; }

        /// <summary>Gets or sets the height in pixels.</summary>
        public long Height { get; set; }

        /// <summary>Gets or sets the bits per component.</summary>
        public int Bits { get; set; }

        /// <summary>Gets or sets the components per pixel; zero when unknown.</summary>
        public int Components { get; set; }

        /// <summary>Gets or sets a value indicating whether the image is a mask.</summary>
        public bool Mask { get; set; }

        /// <summary>Gets or sets a value indicating whether the image has a filter.</summary>
        public bool Filtered { get; set; }

        /// <summary>Stores a numeric value for a key.</summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        public void SetNumber(InlineKey key, long value)
        {
            switch (key)
            {
                case InlineKey.Width:
                {
                    Width = value;
                    break;
                }

                case InlineKey.Height:
                {
                    Height = value;
                    break;
                }

                case InlineKey.Bits:
                {
                    Bits = (int)Math.Clamp(value, 0, MaxBits);
                    break;
                }

                default:
                {
                    break;
                }
            }
        }
    }
}
