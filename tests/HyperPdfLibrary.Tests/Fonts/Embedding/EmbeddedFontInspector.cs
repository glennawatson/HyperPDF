// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Fonts.Embedding;

/// <summary>Reads back the objects of the font a one-page test document embeds.</summary>
internal sealed class EmbeddedFontInspector : IDisposable
{
    /// <summary>The offset of the table count in an sfnt.</summary>
    private const int TableCountOffset = 4;

    /// <summary>The size of the sfnt header.</summary>
    private const int HeaderSize = 12;

    /// <summary>The size of a table record.</summary>
    private const int RecordSize = 16;

    /// <summary>The offset of a record's table offset.</summary>
    private const int RecordOffset = 8;

    /// <summary>The offset of the glyph count in /maxp.</summary>
    private const int MaxpGlyphs = 4;

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>Initializes a new instance of the <see cref="EmbeddedFontInspector"/> class.</summary>
    /// <param name="pdf">The document.</param>
    internal EmbeddedFontInspector(byte[] pdf)
    {
        _document = PdfDocumentReader.Open(pdf, null);
        var names = _document.Objects.Names;
        Type0 = PdfDocumentPages.GetPage(_document, 0).Resources!.GetDictionary(KnownName.Font)!.GetDictionary(names.Intern("F1"))!;
        Descendant = Type0.GetArray(KnownName.DescendantFonts)!.GetDictionary(0)!;
        Descriptor = Descendant.GetDictionary(KnownName.FontDescriptor)!;
    }

    /// <summary>Gets the Type 0 font dictionary.</summary>
    internal PdfDictionary Type0 { get; }

    /// <summary>Gets the CIDFont dictionary.</summary>
    internal PdfDictionary Descendant { get; }

    /// <summary>Gets the font descriptor.</summary>
    internal PdfDictionary Descriptor { get; }

    /// <summary>Gets the name of the font, with its subset tag.</summary>
    internal string BaseFont => _document.Objects.Names.GetString(Type0.GetName(KnownName.BaseFont));

    /// <summary>Gets the decoded <c>/FontFile2</c> program.</summary>
    internal byte[] TrueTypeProgram => Descriptor.GetStream(KnownName.FontFile2)!.DecodeToArray();

    /// <summary>Gets the decoded <c>/FontFile3</c> program.</summary>
    internal byte[] CompactProgram => Descriptor.GetStream(KnownName.FontFile3)!.DecodeToArray();

    /// <summary>Gets the decoded <c>/ToUnicode</c> CMap program as text.</summary>
    internal string ToUnicode => System.Text.Encoding.ASCII.GetString(Type0.GetStream(KnownName.ToUnicode)!.DecodeToArray());

    /// <inheritdoc/>
    public void Dispose() => _document.Dispose();

    /// <summary>Gets the number of glyphs in a TrueType program.</summary>
    /// <param name="font">The font file.</param>
    /// <returns>The glyph count from /maxp.</returns>
    internal static int GlyphCount(byte[] font)
    {
        var count = BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan(TableCountOffset));
        for (var i = 0; i < count; i++)
        {
            var record = HeaderSize + (i * RecordSize);
            if (BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record)) == 0x6D617870)
            {
                return BinaryPrimitives.ReadUInt16BigEndian(font.AsSpan((int)BinaryPrimitives.ReadUInt32BigEndian(font.AsSpan(record + RecordOffset)) + MaxpGlyphs));
            }
        }

        return -1;
    }
}
