// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Fonts.Generation;
using HyperPdfLibrary.Fonts.Programs;
using HyperPdfLibrary.Fonts.Shaping;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks damaged binary font and CMap lengths before they reach runtime bounds checks.</summary>
public sealed class FontBinaryBoundsTests
{
    /// <summary>The bytes of an encoded Private DICT containing one long operand and one operator.</summary>
    private const int PrivateDictBytes = 6;

    /// <summary>The location entries for one glyph and its sentinel.</summary>
    private const int LocationEntries = 2;

    /// <summary>Oversized binary CMap counts fail as malformed resources, not overflowing conversions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsOverflowingBinaryCMapCount() =>
        await Assert.That(static () => BinaryCMapReader.Parse([0, 0x41, 0x8F, 0xFF, 0xFF, 0xFF, 0x7F])).Throws<PdfException>();

    /// <summary>The largest signed count is rejected before reading mapping entries.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsMaximumBinaryCMapCount() =>
        await Assert.That(static () => BinaryCMapReader.Parse([0, 0x41, 0x87, 0xFF, 0xFF, 0xFF, 0x7F])).Throws<PdfException>();

    /// <summary>Oversized metadata lengths fail before conversion and allocation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsOverflowingBinaryCMapMetadataLength() =>
        await Assert.That(static () => BinaryCMapReader.Parse([0, 0xE1, 0x8F, 0xFF, 0xFF, 0xFF, 0x7F])).Throws<PdfException>();

    /// <summary>Metadata characters outside UTF-16 fail as damaged resources.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsOverflowingBinaryCMapMetadataCharacter() =>
        await Assert.That(static () => BinaryCMapReader.Parse([0, 0xE1, 1, 0x87, 0xFF, 0xFF, 0xFF, 0x7F])).Throws<PdfException>();

    /// <summary>CID destinations must fit their decoded integer representation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsOverflowingBinaryCMapDestination() =>
        await Assert.That(static () => BinaryCMapReader.Parse([0, 0x40, 1, 0x41, 0x88, 0x80, 0x80, 0x80, 0])).Throws<PdfException>();

    /// <summary>Negative byte counts fail as damaged packed resources rather than invalid span slices.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsNegativePackedByteCount() =>
        await Assert.That(static () => ReadPackedBytes(-1)).Throws<PdfException>();

    /// <summary>The maximum integer byte count is checked against the remaining bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsMaximumPackedByteCount() =>
        await Assert.That(static () => ReadPackedBytes(int.MaxValue)).Throws<PdfException>();

    /// <summary>Negative fixed-width reads fail before slicing a binary CMap.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsNegativeBinaryCMapHexLength() =>
        await Assert.That(static () => ReadBinaryHex(-1)).Throws<PdfException>();

    /// <summary>Packed numbers cannot silently discard bits beyond their unsigned width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsOverflowingPackedNumber() =>
        await Assert.That(static () => ReadPackedNumber()).Throws<PdfException>();

    /// <summary>Signed-overflow and unbacked CID counts fail before allocating parsed arrays.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsOverflowingPackedCMapRangeCount() =>
        await Assert.That(static () => PredefinedCMaps.Parse(new(), [1, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F])).Throws<PdfException>();

    /// <summary>The maximum signed range count cannot allocate arrays unsupported by the resource bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsUnbackedPackedCMapRangeCount() =>
        await Assert.That(static () => PredefinedCMaps.Parse(new(), [1, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 0x07])).Throws<PdfException>();

    /// <summary>A range length cannot wrap around the maximum unsigned code value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsOverflowingPackedCMapCodeRange() =>
        await Assert.That(static () => PredefinedCMaps.Parse(new(), [1, 0, 0, 0, 0, 0, 1, 0xFF, 0xFF, 0xFF, 0xFF, 0x0F, 1, 0])).Throws<PdfException>();

    /// <summary>A CID delta cannot overflow when the next range continues after the previous range.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RejectsOverflowingPackedCMapCidDelta() =>
        await Assert.That(static () => PredefinedCMaps.Parse(new(), [1, 0, 0, 0, 0, 0, 0x02, 0, 0, 0xFE, 0xFF, 0xFF, 0xFF, 0x0F, 1, 0, 0])).Throws<PdfException>();

    /// <summary>Damaged CFF INDEX offsets stay bounded even when no count can be read.</summary>
    /// <param name="position">The announced INDEX offset.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(int.MaxValue)]
    [Arguments(-1)]
    public async Task RejectsOutOfRangeCffIndex(int position) =>
        await Assert.That(() => CffIndex.Read([0, 0], position, out _)).Throws<PdfException>();

    /// <summary>CFF offsets containing the maximum signed integer cannot select bytes outside the INDEX.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CffIndexRejectsMaximumObjectOffset()
    {
        byte[] data = [0, 1, 4, 0x7F, 0xFF, 0xFF, 0xFF, 0x7F, 0xFF, 0xFF, 0xFF, 0];
        await Assert.That(() => CffIndex.Read(data, 0, out _)).Throws<PdfException>();
    }

    /// <summary>The font recovery helper rejects negative and overflowing ranges without slicing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontSlicesRejectNegativeAndOverflowingRanges()
    {
        byte[] data = [0];
        await Assert.That(() => FontBytes.Slice(data, int.MaxValue, int.MaxValue).Length).Throws<PdfException>();
        await Assert.That(() => FontBytes.Slice(data, -1, 1).Length).Throws<PdfException>();
        await Assert.That(() => FontBytes.Slice(data, 0, -1).Length).Throws<PdfException>();
    }

    /// <summary>Hostile collection directory offsets return an unreadable font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SfntDirectoryRejectsMaximumAndNegativeFaceOffsets()
    {
        byte[] maximum = [0x74, 0x74, 0x63, 0x66, 0, 1, 0, 0, 0, 0, 0, 1, 0x7F, 0xFF, 0xFF, 0xFF];
        byte[] negative = [0x74, 0x74, 0x63, 0x66, 0, 1, 0, 0, 0, 0, 0, 1, 0xFF, 0xFF, 0xFF, 0xFF];
        await Assert.That(() => SfntDirectory.TryRead(maximum, out _)).Throws<PdfException>();
        await Assert.That(() => SfntDirectory.TryRead(negative, out _)).Throws<PdfException>();
    }

    /// <summary>TrueType cmap offsets outside the font select no supported table.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TrueTypeCmapRejectsMaximumAndNegativeSubtableOffsets()
    {
        byte[] maximum = [0, 0, 0, 1, 0, 3, 0, 1, 0x7F, 0xFF, 0xFF, 0xFF];
        byte[] negative = [0, 0, 0, 1, 0, 3, 0, 1, 0xFF, 0xFF, 0xFF, 0xFF];
        await Assert.That(() => CmapSelection.Read(maximum, new(0, maximum.Length))).Throws<PdfException>();
        await Assert.That(() => CmapSelection.Read(negative, new(0, negative.Length))).Throws<PdfException>();
    }

    /// <summary>Private DICT offsets cannot wrap when adding a relative subroutine offset.</summary>
    /// <param name="relative">The encoded relative subroutine offset.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(int.MaxValue)]
    [Arguments(-1)]
    public async Task CffPrivateRejectsInvalidRelativeOffset(int relative) =>
        await Assert.That(() => CffPrivate.RelativeOffset([0], 1, relative)).Throws<PdfException>();

    /// <summary>A declared negative Subrs offset is malformed, not an absent subroutine table.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CffPrivateRejectsNegativeEncodedSubroutineOffset() =>
        await Assert.That(static () => CffPrivate.Read([0, 0x1D, 0xFF, 0xFF, 0xFF, 0xFF, 0x13], PrivateDictBytes, 1)).Throws<PdfException>();

    /// <summary>The maximum encoded Subrs offset is bounded before adding its Private DICT base.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CffPrivateRejectsMaximumEncodedSubroutineOffset() =>
        await Assert.That(static () => CffPrivate.Read([0, 0x1D, 0x7F, 0xFF, 0xFF, 0xFF, 0x13], PrivateDictBytes, 1)).Throws<PdfException>();

    /// <summary>A negative encoded CharStrings offset is rejected at the DICT boundary.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CffTopDictRejectsNegativeEncodedCharstringsOffset() =>
        await Assert.That(static () => CffTopDict.Read([0x1D, 0xFF, 0xFF, 0xFF, 0xFF, 0x11])).Throws<PdfException>();

    /// <summary>CFF charset offsets fail before allocating or walking glyph records.</summary>
    /// <param name="offset">The encoded charset offset.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(int.MaxValue)]
    [Arguments(-1)]
    public async Task CffCharsetRejectsInvalidOffset(int offset) =>
        await Assert.That(() => CffCharsets.ReadCharset([0], offset, 1, true)).Throws<PdfException>();

    /// <summary>CFF FDSelect offsets fail before array access.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CffFdSelectRejectsMaximumOffset() =>
        await Assert.That(static () => CffCharsets.ReadFdSelect([0], int.MaxValue, 1, 1)).Throws<PdfException>();

    /// <summary>PFB segment lengths are checked without clamping corrupted binary lengths.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Type1RejectsMaximumSegmentLength() =>
        await Assert.That(static () => Type1Parser.TryParse([0x80, 1, 0xFF, 0xFF, 0xFF, 0x7F], 0, out _)).Throws<PdfException>();

    /// <summary>Collection face counts cannot allocate unbacked directories.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SfntFontRejectsMaximumFaceCount() =>
        await Assert.That(static () => SfntFont.TryRead(
            [0x74, 0x74, 0x63, 0x66, 0, 1, 0, 0, 0x7F, 0xFF, 0xFF, 0xFF],
            0,
            out _,
            out _)).Throws<PdfException>();

    /// <summary>Collection face offsets cannot select bytes outside the font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SfntFontRejectsMaximumFaceOffset() =>
        await Assert.That(static () => SfntFont.TryRead(
            [0x74, 0x74, 0x63, 0x66, 0, 1, 0, 0, 0, 0, 0, 1, 0x7F, 0xFF, 0xFF, 0xFF],
            0,
            out _,
            out _)).Throws<PdfException>();

    /// <summary>TrueType location counts fail before allocation and count-plus-one arithmetic.</summary>
    /// <param name="count">The announced glyph count.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(int.MaxValue)]
    [Arguments(-1)]
    public async Task TrueTypeLocationsRejectInvalidCount(int count) =>
        await Assert.That(() => TrueTypeLocations.TryRead([0, 0], 0, count, false, out _)).Throws<PdfException>();

    /// <summary>Hostile long loca offsets fail as malformed font data before converting the offset to a signed integer.</summary>
    /// <param name="offset">The signed bits written into the unsigned location field.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(int.MaxValue)]
    [Arguments(-1)]
    public async Task TrueTypeLocationsRejectOutOfRangeGlyphOffset(int offset)
    {
        var loca = new byte[TrueTypeLocations.LongEntry * LocationEntries];
        BinaryPrimitives.WriteInt32BigEndian(loca.AsSpan(TrueTypeLocations.LongEntry), offset);
        await Assert.That(() => TrueTypeLocations.TryRead(loca, 0, 1, true, out _)).Throws<PdfException>();
    }

    /// <summary>Coverage group counts cannot silently truncate a corrupt table.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TrueTypeCmapRejectsMaximumGroupCount() =>
        await Assert.That(static () => TrueTypeCmap.Lookup(
            [0, 0x0C, 0, 0, 0, 0, 0, 0x10, 0, 0, 0, 0, 0x7F, 0xFF, 0xFF, 0xFF],
            0,
            0)).Throws<PdfException>();

    /// <summary>Unsigned glyph-base arithmetic cannot wrap to a valid glyph.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TrueTypeCmapRejectsOverflowingGlyphRange() =>
        await Assert.That(static () => TrueTypeCmap.Lookup(
            [0, 0x0C, 0, 0, 0, 0, 0, 0x1C, 0, 0, 0, 0, 0, 0, 0, 1,
             0, 0, 0, 0, 0, 0, 0, 1, 0xFF, 0xFF, 0xFF, 0xFF],
            0,
            1)).Throws<PdfException>();

    /// <summary>Text CMap CID destinations fail before range offsets exceed the signed width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextCmapRejectsOverflowingCidRange() =>
        await Assert.That(static () => CMapParser.Parse(
            "1 begincidrange <00000000> <7fffffff> 2147483647 endcidrange"u8)).Throws<PdfException>();

    /// <summary>Negative CID destinations do not escape into glyph indexing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextCmapRejectsNegativeCid() =>
        await Assert.That(static () => CMapParser.Parse("1 begincidchar <01> -1 endcidchar"u8)).Throws<PdfException>();

    /// <summary>A constant notdef range can span all unsigned codes without increasing its destination CID.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextCmapPreservesWideConstantNotdefRange()
    {
        var content = CMapParser.Parse("1 beginnotdefrange <00000000> <ffffffff> 0 endnotdefrange"u8);
        await Assert.That(content.Notdefs.Count).IsEqualTo(1);
    }

    /// <summary>An array mapping after the maximum code cannot wrap around and shadow code zero.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextCmapArrayDoesNotWrapMaximumCode()
    {
        var content = CMapParser.Parse("1 beginbfrange <ffffffff> <ffffffff> [<0041> <0042>] endbfrange"u8);
        await Assert.That(content.Unicode.Count).IsEqualTo(1);
    }

    /// <summary>Extension subtable offsets are bounded before adding them to the extension header.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenTypeExtensionRejectsMaximumRelativeOffset()
    {
        byte[] table =
            [0, 1, 0, 0, 0, 0, 0, 0, 0, 0x0A,
             0, 1, 0, 0x04,
             0, 0x07, 0, 0, 0, 1, 0, 0x08,
             0, 1, 0, 1, 0x7F, 0xFF, 0xFF, 0xFF];
        using var glyphs = new GlyphBuffer(1);
        glyphs.Add(new(1, 0, 0));
        var engine = new LayoutEngine(new(table), new(ReadOnlyMemory<byte>.Empty), glyphs, false, false);
        await Assert.That(() => engine.RunStage([new(0, 1, 1)])).Throws<PdfException>();
    }

    /// <summary>Mark-set coverage offsets are bounded before adding their mark-set table base.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenTypeMarkSetRejectsMaximumRelativeOffset()
    {
        byte[] table =
            [0, 1, 0, 0x02, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0x0E,
             0, 1, 0, 1, 0x7F, 0xFF, 0xFF, 0xFF];
        var gdef = new OtGdef(table);
        await Assert.That(() => gdef.IsInMarkSet(0, 1)).Throws<PdfException>();
    }

    /// <summary>Reads one packed resource range.</summary>
    /// <param name="count">The byte count.</param>
    /// <returns>The byte count read.</returns>
    private static int ReadPackedBytes(int count)
    {
        var reader = new PackedDataReader([0]);
        return reader.ReadBytes(count).Length;
    }

    /// <summary>Reads a packed unsigned number containing more than thirty-two value bits.</summary>
    private static void ReadPackedNumber()
    {
        var reader = new PackedDataReader([0xFF, 0xFF, 0xFF, 0xFF, 0x7F]);
        _ = reader.ReadNumber();
    }

    /// <summary>Reads one fixed-width binary CMap value.</summary>
    /// <param name="length">The byte count.</param>
    private static void ReadBinaryHex(int length)
    {
        var reader = new BinaryCMapStream([0]);
        _ = reader.ReadHex(length);
    }
}
