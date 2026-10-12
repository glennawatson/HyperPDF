// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Loads the embedded program of a used font the way veraPDF selects it: Type 1 fonts read <c>/FontFile</c>, or a
/// Type1C or OpenType <c>/FontFile3</c>; TrueType fonts read <c>/FontFile2</c>, else <c>/FontFile3</c> as TrueType;
/// CIDFonts read <c>/FontFile2</c> when they are CIDFontType2, else a CIDFontType0C or OpenType <c>/FontFile3</c>.
/// </summary>
internal static class PdfAFontPrograms
{
    /// <summary>The 'CFF ' table tag.</summary>
    private const long CffTag = 0x43464620;

    /// <summary>The bytes before the table count in the offset table.</summary>
    private const int VersionBytes = 4;

    /// <summary>The bytes after the table count in the offset table.</summary>
    private const int SearchBytes = 6;

    /// <summary>Gets a used font's program, loading it once.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="font">The font.</param>
    /// <returns>The program; for a composite font, the descendant CIDFont's.</returns>
    internal static PdfAFontProgram Get(PdfAValidationContext context, PdfAFontUse font)
    {
        if (font.Program is { } known)
        {
            return known;
        }

        var program = font.Kind switch
        {
            PdfAFontKind.Type1 => LoadType1(context, PdfAFontFacts.Descriptor(font.Dictionary)),
            PdfAFontKind.TrueType => LoadTrueType(context, font.Dictionary),
            PdfAFontKind.Type0 when PdfAFontFacts.Descendant(font.Dictionary) is { } descendant => LoadCid(context, descendant),
            _ => PdfAFontProgram.Missing,
        };
        font.Program = program;
        return program;
    }

    /// <summary>Loads a Type 1 font's program.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="descriptor">The descriptor, or null.</param>
    /// <returns>The program.</returns>
    private static PdfAFontProgram LoadType1(PdfAValidationContext context, PdfDictionary? descriptor)
    {
        if (descriptor?.GetStream(KnownName.FontFile) is { } type1)
        {
            if (!PdfAFontStreams.TryDecode(type1, context.CancellationToken, out var data))
            {
                return PdfAFontProgram.Missing;
            }

            return Type1Program.TryParse(data, out var program) ? new(true, true) { Type1 = program } : PdfAFontProgram.Broken;
        }

        return descriptor?.GetStream(KnownName.FontFile3) is { } compact && compact.Dictionary.Get(KnownName.Subtype).AsName().ToKnownName() is KnownName.Type1C or KnownName.OpenType
            ? LoadCff(context, compact)
            : PdfAFontProgram.Missing;
    }

    /// <summary>
    /// Loads a TrueType font's program. A non-symbolic font whose /Encoding the program cannot turn into a name table
    /// fails, as veraPDF's program does.
    /// </summary>
    /// <param name="context">The validation.</param>
    /// <param name="font">The font dictionary.</param>
    /// <returns>The program.</returns>
    private static PdfAFontProgram LoadTrueType(PdfAValidationContext context, PdfDictionary font)
    {
        var descriptor = PdfAFontFacts.Descriptor(font);
        var stream = descriptor?.GetStream(KnownName.FontFile2) ?? descriptor?.GetStream(KnownName.FontFile3);
        if (stream is null)
        {
            return PdfAFontProgram.Missing;
        }

        var program = ReadTrueType(context, stream);
        var encoding = font.Get(KnownName.Encoding);
        var namesFail = program.Parsed && !PdfAFontFacts.IsSymbolic(descriptor) && !encoding.IsNull && !PdfATrueTypeNames.CanBuild(encoding);
        return namesFail ? PdfAFontProgram.Broken : program;
    }

    /// <summary>Loads a CIDFont's program.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="descendant">The CIDFont dictionary.</param>
    /// <returns>The program.</returns>
    private static PdfAFontProgram LoadCid(PdfAValidationContext context, PdfDictionary descendant)
    {
        var descriptor = PdfAFontFacts.Descriptor(descendant);
        var subtype = descendant.GetName(KnownName.Subtype).ToKnownName();
        if (subtype == KnownName.CIDFontType2 && descriptor?.GetStream(KnownName.FontFile2) is { } trueType)
        {
            return ReadTrueType(context, trueType);
        }

        var compact = descriptor?.GetStream(KnownName.FontFile3);
        return compact?.Dictionary.Get(KnownName.Subtype).AsName().ToKnownName() switch
        {
            null => PdfAFontProgram.Missing,
            KnownName.OpenType when subtype is KnownName.TrueType or KnownName.CIDFontType2 => ReadTrueType(context, compact),
            KnownName.CIDFontType0C or KnownName.OpenType => LoadCff(context, compact),
            _ => PdfAFontProgram.Missing,
        };
    }

    /// <summary>Reads a TrueType program, or an OpenType program read as TrueType.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="stream">The program stream.</param>
    /// <returns>The program.</returns>
    private static PdfAFontProgram ReadTrueType(PdfAValidationContext context, PdfStream stream)
    {
        if (!PdfAFontStreams.TryDecode(stream, context.CancellationToken, out var data))
        {
            return PdfAFontProgram.Missing;
        }

        return PdfATrueTypeReader.TryRead(data, out var tables) ? new(true, true) { TrueType = tables } : PdfAFontProgram.Broken;
    }

    /// <summary>Reads a bare CFF program, or the 'CFF ' table of an OpenType program.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="stream">The program stream.</param>
    /// <returns>The program.</returns>
    private static PdfAFontProgram LoadCff(PdfAValidationContext context, PdfStream stream)
    {
        if (!PdfAFontStreams.TryDecode(stream, context.CancellationToken, out var data))
        {
            return PdfAFontProgram.Missing;
        }

        ReadOnlyMemory<byte> table = data;
        if (stream.Dictionary.IsName(KnownName.Subtype, KnownName.OpenType) && !TryFindCffTable(data, out table))
        {
            return PdfAFontProgram.Broken;
        }

        return CffProgram.TryParse(table, out var program) ? new(true, true) { Cff = program } : PdfAFontProgram.Broken;
    }

    /// <summary>Finds the 'CFF ' table of an OpenType program.</summary>
    /// <param name="data">The program.</param>
    /// <param name="table">The table.</param>
    /// <returns><see langword="true"/> when the table is found within the data.</returns>
    private static bool TryFindCffTable(byte[] data, out ReadOnlyMemory<byte> table)
    {
        table = default;
        try
        {
            var cursor = new PdfAFontCursor(data);
            cursor.Skip(VersionBytes);
            var count = cursor.ReadUInt16();
            cursor.Skip(SearchBytes);
            for (var i = 0; i < count; i++)
            {
                var tag = cursor.ReadUInt32();
                _ = cursor.ReadUInt32();
                var offset = cursor.ReadUInt32();
                var length = cursor.ReadUInt32();
                if (tag != CffTag || offset + length > data.Length)
                {
                    continue;
                }

                table = data.AsMemory((int)offset, (int)length);
                return true;
            }
        }
        catch (InvalidDataException)
        {
            return false;
        }

        return false;
    }
}
