// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// Parses the font program a font descriptor embeds. The descriptor key picks the parser, but writers often put a
/// program of another kind under a key, so the data's signature decides when the expected parser fails.
/// </summary>
internal static class EmbeddedFontLoader
{
    /// <summary>The smallest program worth parsing.</summary>
    private const int MinProgramLength = 4;

    /// <summary>The major version byte that starts a CFF table.</summary>
    private const byte CffMajor = 1;

    /// <summary>The marker byte that starts a PFB segment.</summary>
    private const byte PfbMarker = 0x80;

    /// <summary>Loads an embedded program.</summary>
    /// <param name="descriptor">The font descriptor.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The program, or <see langword="null"/> when the font is not embedded or its program cannot be read.</returns>
    internal static FontProgram? Load(FontDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        if (descriptor.FontFile is not { } stream)
        {
            return null;
        }

        try
        {
            var data = stream.DecodeToArray(cancellationToken);
            return data.Length < MinProgramLength ? null : Parse(data, descriptor.FileKind, ReadLength1(stream));
        }
        catch (Exception ex) when (FontLoadErrors.IsDamagedData(ex))
        {
            return null;
        }
    }

    /// <summary>Parses program data, trying the expected kind first and then the kind its signature shows.</summary>
    /// <param name="data">The decoded program.</param>
    /// <param name="kind">The descriptor key.</param>
    /// <param name="length1">The /Length1 of a Type 1 program, or zero.</param>
    /// <returns>The program, or <see langword="null"/>.</returns>
    internal static FontProgram? Parse(byte[] data, FontFileKind kind, int length1)
    {
        var expected = kind switch
        {
            FontFileKind.Type1 => ParseType1(data, length1),
            FontFileKind.TrueType => ParseSfnt(data),
            FontFileKind.Compact => ParseCompact(data),
            _ => null,
        };
        return expected ?? ParseBySignature(data, length1);
    }

    /// <summary>Parses by the data's leading bytes.</summary>
    /// <param name="data">The data.</param>
    /// <param name="length1">The /Length1, or zero.</param>
    /// <returns>The program, or <see langword="null"/>.</returns>
    private static FontProgram? ParseBySignature(byte[] data, int length1)
    {
        if (data[0] == CffMajor && data[1] == 0)
        {
            return ParseCompact(data);
        }

        return data[0] == (byte)'%' || data[0] == PfbMarker ? ParseType1(data, length1) : ParseSfnt(data) ?? ParseCompact(data) ?? ParseType1(data, length1);
    }

    /// <summary>Parses a Type 1 program.</summary>
    /// <param name="data">The data.</param>
    /// <param name="length1">The cleartext length, or zero.</param>
    /// <returns>The program, or <see langword="null"/>.</returns>
    private static Type1Program? ParseType1(byte[] data, int length1) =>
        Type1Program.TryParse(data, length1, out var program) && program.GlyphCount > 0 ? program : null;

    /// <summary>Parses a TrueType or OpenType program.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The program, or <see langword="null"/>.</returns>
    private static TrueTypeProgram? ParseSfnt(byte[] data) =>
        TrueTypeProgram.TryParse(data, out var program) && program.GlyphCount > 0 ? program : null;

    /// <summary>Parses a bare CFF program, or an OpenType program written under /FontFile3.</summary>
    /// <param name="data">The data.</param>
    /// <returns>The program, or <see langword="null"/>.</returns>
    private static FontProgram? ParseCompact(byte[] data) =>
        data[0] == CffMajor && CffProgram.TryParse(data, out var cff) ? cff : ParseSfnt(data);

    /// <summary>Reads a Type 1 stream's /Length1.</summary>
    /// <param name="stream">The stream.</param>
    /// <returns>The length, or zero.</returns>
    private static int ReadLength1(PdfStream stream)
    {
        var dictionary = stream.Dictionary;
        var names = dictionary.Owner?.Names;
        return names is null ? 0 : Math.Max(dictionary.GetInt32(names.Intern("Length1"u8)), 0);
    }
}
