// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>
/// Builds PDF/A files with <see cref="PdfDocumentBuilder"/>: a vector page in DeviceRGB, an sRGB-like monitor output
/// intent and an XMP packet that claims the level. The files hold no text, so they need no fonts.
/// </summary>
internal static class PdfAFixtures
{
    /// <summary>The XMP title of a new fixture.</summary>
    internal const string Title = "HyperPdf PDF/A fixture";

    /// <summary>The XMP producer and creator tool of a new fixture.</summary>
    internal const string Producer = "HyperPdfLibrary tests";

    /// <summary>The XMP creation and modification date of a new fixture.</summary>
    internal const string Date = "2026-10-12T09:30:00+11:00";

    /// <summary>The page size in points.</summary>
    internal const int PageSize = 200;

    /// <summary>The PDF/A part based on PDF 1.4.</summary>
    private const int Part1 = 1;

    /// <summary>The PDF/A part based on PDF 2.0, which has a revision and no conformance letter.</summary>
    private const int Part4 = 4;

    /// <summary>The PDF/A-4 revision year.</summary>
    private const int Part4Revision = 2020;

    /// <summary>The components of the RGB output profile.</summary>
    private const int RgbComponents = 3;

    /// <summary>The spaces that pad the XMP packet so it can be edited in place.</summary>
    private const int Padding = 512;

    /// <summary>The ICC profile header size.</summary>
    private const int HeaderSize = 128;

    /// <summary>The bytes of the tag count before the tag table.</summary>
    private const int CountSize = 4;

    /// <summary>The bytes of one tag table entry: signature, offset and size.</summary>
    private const int TagEntrySize = 12;

    /// <summary>The offset of a tag's data offset within its table entry.</summary>
    private const int TagOffsetField = 4;

    /// <summary>The offset of a tag's size within its table entry.</summary>
    private const int TagSizeField = 8;

    /// <summary>The bytes every tag type starts with: its signature and four reserved bytes.</summary>
    private const int TypeHeader = 8;

    /// <summary>The offset of the text in a v2 'desc' tag, after its length.</summary>
    private const int DescriptionText = 12;

    /// <summary>The bytes after a v2 'desc' tag's ASCII text: empty Unicode and ScriptCode parts and 67 reserved bytes.</summary>
    private const int DescriptionTail = 78;

    /// <summary>The offset of the gamma in a one-entry 'curv' tag, after the entry count.</summary>
    private const int CurveGamma = 12;

    /// <summary>The size of a one-entry 'curv' tag.</summary>
    private const int CurveSize = 14;

    /// <summary>The u8Fixed8 gamma of the tone curves, 2.2.</summary>
    private const ushort Gamma22 = 0x0233;

    /// <summary>The ICC version 2.4, the newest PDF/A-1 allows.</summary>
    private const uint IccVersion = 0x0240_0000;

    /// <summary>The offset of the profile version in the header.</summary>
    private const int VersionOffset = 8;

    /// <summary>The offset of the device class in the header.</summary>
    private const int ClassOffset = 12;

    /// <summary>The offset of the colour space in the header.</summary>
    private const int ColorSpaceOffset = 16;

    /// <summary>The offset of the connection space in the header.</summary>
    private const int ConnectionOffset = 20;

    /// <summary>The offset of the file signature in the header.</summary>
    private const int SignatureOffset = 36;

    /// <summary>The offset of the illuminant in the header.</summary>
    private const int IlluminantOffset = 68;

    /// <summary>Gets the output intent's condition identifier as bytes.</summary>
    private static ReadOnlySpan<byte> ConditionBytes => "sRGB IEC61966-2.1"u8;

    /// <summary>Gets the D50 white point as three s15Fixed16 numbers.</summary>
    private static ReadOnlySpan<byte> WhitePoint => [0x00, 0x00, 0xF6, 0xD6, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0xD3, 0x2D];

    /// <summary>Gets the red primary of sRGB adapted to D50 (0.4361, 0.2225, 0.0139) as s15Fixed16 numbers.</summary>
    private static ReadOnlySpan<byte> RedPrimary => [0x00, 0x00, 0x6F, 0xA4, 0x00, 0x00, 0x38, 0xF6, 0x00, 0x00, 0x03, 0x8F];

    /// <summary>Gets the green primary of sRGB adapted to D50 (0.3851, 0.7169, 0.0971) as s15Fixed16 numbers.</summary>
    private static ReadOnlySpan<byte> GreenPrimary => [0x00, 0x00, 0x62, 0x96, 0x00, 0x00, 0xB7, 0x87, 0x00, 0x00, 0x18, 0xDC];

    /// <summary>Gets the blue primary of sRGB adapted to D50 (0.1431, 0.0606, 0.7141) as s15Fixed16 numbers.</summary>
    private static ReadOnlySpan<byte> BluePrimary => [0x00, 0x00, 0x24, 0xA2, 0x00, 0x00, 0x0F, 0x83, 0x00, 0x00, 0xB6, 0xCF];

    /// <summary>Builds a fixture that claims a PDF/A level.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The file bytes.</returns>
    internal static byte[] Create(PdfAFixtureLevel level)
    {
        var builder = new PdfDocumentBuilder { Version = HeaderVersion(level.Part) };
        var names = builder.Names;
        var profile = new PdfDictionary(null);
        profile.Set(KnownName.N, PdfValue.FromInteger(RgbComponents));
        var profileId = builder.Add(PdfValue.FromStream(new(profile, OutputProfile())));
        var intent = new PdfDictionary(null);
        intent.Set(KnownName.Type, PdfValue.FromName(names.Intern("OutputIntent"u8)));
        intent.Set(names.Intern("S"u8), PdfValue.FromName(names.Intern("GTS_PDFA1"u8)));
        intent.Set(names.Intern("OutputConditionIdentifier"u8), PdfValue.FromString(ConditionBytes.ToArray()));
        intent.Set(KnownName.Info, PdfValue.FromString(ConditionBytes.ToArray()));
        intent.Set(names.Intern("DestOutputProfile"u8), PdfValue.FromReference(profileId));
        var metadata = new PdfDictionary(null);
        metadata.Set(KnownName.Type, PdfValue.FromName(KnownName.Metadata));
        metadata.Set(KnownName.Subtype, PdfValue.FromName(names.Intern("XML"u8)));
        var metadataId = builder.Add(PdfValue.FromStream(new(metadata, Packet(level))));
        builder.CatalogEntries.Set(KnownName.Metadata, PdfValue.FromReference(metadataId));
        builder.CatalogEntries.Set(names.Intern("OutputIntents"u8), PdfValue.FromArray(new(null, [PdfValue.FromDictionary(intent)])));
        var contentId = builder.Add(PdfValue.FromStream(new(new PdfDictionary(null), "0.2 0.4 0.6 rg 20 20 160 160 re f\n0 0 0 RG 2 w 20 20 m 180 180 l S\n"u8.ToArray())));
        var page = new PdfDictionary(null);
        page.Set(KnownName.MediaBox, PdfValue.FromArray(new(null, [PdfValue.FromInteger(0), PdfValue.FromInteger(0), PdfValue.FromInteger(PageSize), PdfValue.FromInteger(PageSize)])));
        page.Set(KnownName.Resources, PdfValue.FromDictionary(new(null)));
        page.Set(KnownName.Contents, PdfValue.FromReference(contentId));
        _ = builder.AddPage(page);
        return builder.ToArray();
    }

    /// <summary>Gets the header version a part is based on.</summary>
    /// <param name="part">The PDF/A part.</param>
    /// <returns>The version.</returns>
    private static PdfHeaderVersion HeaderVersion(int part) => part switch
    {
        Part1 => PdfHeaderVersion.Pdf14,
        Part4 => PdfHeaderVersion.Pdf20,
        _ => PdfHeaderVersion.Pdf17,
    };

    /// <summary>Writes the XMP packet: the identification, title, producer, tool and dates.</summary>
    /// <param name="level">The level claimed.</param>
    /// <returns>The UTF-8 packet.</returns>
    private static byte[] Packet(PdfAFixtureLevel level)
    {
        var identification = level.Part == Part4
            ? string.Create(CultureInfo.InvariantCulture, $"<pdfaid:part>{Part4}</pdfaid:part><pdfaid:rev>{Part4Revision}</pdfaid:rev>")
            : string.Create(CultureInfo.InvariantCulture, $"<pdfaid:part>{level.Part}</pdfaid:part><pdfaid:conformance>{level.Conformance}</pdfaid:conformance>");
        var packet = "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">\n"
            + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n"
            + "<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\""
            + " xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\">\n"
            + $"<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">{Title}</rdf:li></rdf:Alt></dc:title>\n"
            + $"<xmp:CreatorTool>{Producer}</xmp:CreatorTool><xmp:CreateDate>{Date}</xmp:CreateDate><xmp:ModifyDate>{Date}</xmp:ModifyDate>\n"
            + $"<pdf:Producer>{Producer}</pdf:Producer>\n{identification}\n</rdf:Description>\n</rdf:RDF>\n</x:xmpmeta>\n"
            + new string(' ', Padding) + "\n<?xpacket end=\"w\"?>";
        return Encoding.UTF8.GetBytes(packet);
    }

    /// <summary>Writes a v2.4 RGB monitor profile with sRGB primaries adapted to D50 and gamma 2.2 curves.</summary>
    /// <returns>The profile bytes.</returns>
    private static byte[] OutputProfile()
    {
        var curve = Curve();
        Tag[] tags =
        [
            new("desc", Description("HyperPdf test RGB")),
            new("cprt", Text("No copyright, test data")),
            new("wtpt", Xyz(WhitePoint)),
            new("rXYZ", Xyz(RedPrimary)),
            new("gXYZ", Xyz(GreenPrimary)),
            new("bXYZ", Xyz(BluePrimary)),
            new("rTRC", curve),
            new("gTRC", curve),
            new("bTRC", curve),
        ];
        var offset = HeaderSize + CountSize + (tags.Length * TagEntrySize);
        var total = offset;
        foreach (var tag in tags)
        {
            total += Aligned(tag.Data.Length);
        }

        var profile = new byte[total];
        BinaryPrimitives.WriteUInt32BigEndian(profile, (uint)total);
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(VersionOffset), IccVersion);
        "mntr"u8.CopyTo(profile.AsSpan(ClassOffset));
        "RGB "u8.CopyTo(profile.AsSpan(ColorSpaceOffset));
        "XYZ "u8.CopyTo(profile.AsSpan(ConnectionOffset));
        "acsp"u8.CopyTo(profile.AsSpan(SignatureOffset));
        WhitePoint.CopyTo(profile.AsSpan(IlluminantOffset));
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(HeaderSize), (uint)tags.Length);
        for (var i = 0; i < tags.Length; i++)
        {
            var entry = profile.AsSpan(HeaderSize + CountSize + (i * TagEntrySize));
            Encoding.ASCII.GetBytes(tags[i].Signature).CopyTo(entry);
            BinaryPrimitives.WriteUInt32BigEndian(entry[TagOffsetField..], (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(entry[TagSizeField..], (uint)tags[i].Data.Length);
            tags[i].Data.CopyTo(profile, offset);
            offset += Aligned(tags[i].Data.Length);
        }

        return profile;
    }

    /// <summary>Writes a v2 'desc' tag holding ASCII text.</summary>
    /// <param name="text">The description.</param>
    /// <returns>The tag data.</returns>
    private static byte[] Description(string text)
    {
        var data = new byte[DescriptionText + text.Length + 1 + DescriptionTail];
        "desc"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(TypeHeader), (uint)(text.Length + 1));
        Encoding.ASCII.GetBytes(text).CopyTo(data, DescriptionText);
        return data;
    }

    /// <summary>Writes a 'text' tag.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The tag data.</returns>
    private static byte[] Text(string text)
    {
        var data = new byte[TypeHeader + text.Length + 1];
        "text"u8.CopyTo(data);
        Encoding.ASCII.GetBytes(text).CopyTo(data, TypeHeader);
        return data;
    }

    /// <summary>Writes an 'XYZ ' tag.</summary>
    /// <param name="xyz">The three s15Fixed16 numbers.</param>
    /// <returns>The tag data.</returns>
    private static byte[] Xyz(ReadOnlySpan<byte> xyz)
    {
        var data = new byte[TypeHeader + xyz.Length];
        "XYZ "u8.CopyTo(data);
        xyz.CopyTo(data.AsSpan(TypeHeader));
        return data;
    }

    /// <summary>Writes a 'curv' tag with one gamma.</summary>
    /// <returns>The tag data.</returns>
    private static byte[] Curve()
    {
        var data = new byte[CurveSize];
        "curv"u8.CopyTo(data);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(TypeHeader), 1);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(CurveGamma), Gamma22);
        return data;
    }

    /// <summary>Rounds a tag length up to a four-byte boundary.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The aligned length.</returns>
    private static int Aligned(int length) => (length + CountSize - 1) / CountSize * CountSize;

    /// <summary>One profile tag.</summary>
    /// <param name="Signature">The four-letter signature.</param>
    /// <param name="Data">The tag data.</param>
    private sealed record Tag(string Signature, byte[] Data);
}
