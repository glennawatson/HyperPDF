// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>Marker segment rules of ISO 15444-1 annex A that the header state applies.</summary>
public sealed class JpxHeaderStateTests
{
    /// <summary>The offset the first test segment starts at.</summary>
    private const int FirstOffset = 100;

    /// <summary>The offset the second test segment starts at.</summary>
    private const int SecondOffset = 200;

    /// <summary>The offset the third test segment starts at.</summary>
    private const int ThirdOffset = 300;

    /// <summary>The shift of the test RGN segments.</summary>
    private const byte Shift = 9;

    /// <summary>The Zppt of a segment that follows the one numbered zero.</summary>
    private const byte LaterIndex = 1;

    /// <summary>The data bytes of the short test segment.</summary>
    private const int ShortLength = 2;

    /// <summary>The data bytes of the long test segment.</summary>
    private const int LongLength = 3;

    /// <summary>The count of segments the ordering test reads.</summary>
    private const int TwoSegments = 2;

    /// <summary>The components of the wide-index test.</summary>
    private const int WideComponents = 300;

    /// <summary>The component the wide-index RGN segment names: 0x0100.</summary>
    private const int WideIndex = 256;

    /// <summary>An RGN segment for component 256 with a two-byte index: the method 0 and a shift.</summary>
    private static readonly byte[] WideRegion = [1, 0, 0, Shift];

    /// <summary>Gets the SOT marker.</summary>
    private static ReadOnlySpan<byte> StartOfTile => [0xFF, 0x90];

    /// <summary>Gets a reserved marker with no segment.</summary>
    private static ReadOnlySpan<byte> BareMarker => [0xFF, 0x30];

    /// <summary>PPT segments are joined in order of their Zppt, not in the order they appear.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PackedSegmentsAreOrderedByIndex()
    {
        var state = new JpxHeaderState(1);
        state.BeginTilePart();
        _ = state.Read(JpxMarkers.PackedTileHeaders, Segment(LaterIndex, LongLength), FirstOffset);
        _ = state.Read(JpxMarkers.PackedTileHeaders, Segment(0, ShortLength), SecondOffset);

        var pieces = state.PackedHeaders!;
        await Assert.That(pieces.Count).IsEqualTo(TwoSegments);
        await Assert.That(pieces[0].Offset).IsEqualTo(SecondOffset + 1);
        await Assert.That(pieces[0].Length).IsEqualTo(ShortLength);
        await Assert.That(pieces[1].Offset).IsEqualTo(FirstOffset + 1);
        await Assert.That(pieces[1].Length).IsEqualTo(LongLength);
    }

    /// <summary>The PPT numbering restarts in each tile-part header, so earlier tile-parts stay first.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PackedSegmentsOfEarlierTilePartsStayFirst()
    {
        var state = new JpxHeaderState(1);
        state.BeginTilePart();
        _ = state.Read(JpxMarkers.PackedTileHeaders, Segment(LaterIndex, ShortLength), FirstOffset);
        state.BeginTilePart();
        _ = state.Read(JpxMarkers.PackedTileHeaders, Segment(0, ShortLength), SecondOffset);
        _ = state.Read(JpxMarkers.PackedTileHeaders, Segment(LaterIndex, ShortLength), ThirdOffset);

        var pieces = state.PackedHeaders!;
        await Assert.That(pieces[0].Offset).IsEqualTo(FirstOffset + 1);
        await Assert.That(pieces[1].Offset).IsEqualTo(SecondOffset + 1);
        await Assert.That(pieces[TwoSegments].Offset).IsEqualTo(ThirdOffset + 1);
    }

    /// <summary>An RGN segment sets the shift only when its Srgn is 0, the implicit maxshift method.</summary>
    /// <param name="method">The Srgn value.</param>
    /// <param name="expected">The shift the state holds afterwards, or -1 for none.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments((byte)0, (int)Shift)]
    [Arguments((byte)1, -1)]
    public async Task RegionOfInterestNeedsTheMaxshiftMethod(byte method, int expected)
    {
        var state = new JpxHeaderState(1);

        var valid = state.Read(JpxMarkers.RegionOfInterest, new byte[] { 0, method, Shift }, 0);

        await Assert.That(valid).IsTrue();
        await Assert.That(state.RoiShifts[0]).IsEqualTo(expected);
    }

    /// <summary>With more than 256 components the component index of an RGN segment takes two bytes (A.6.3).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WideComponentIndexIsReadInRegionSegments()
    {
        var state = new JpxHeaderState(WideComponents);

        var valid = state.Read(JpxMarkers.RegionOfInterest, WideRegion, 0);

        await Assert.That(valid).IsTrue();
        await Assert.That(state.RoiShifts[WideIndex]).IsEqualTo(Shift);
    }

    /// <summary>A reserved marker from 0xFF30 to 0xFF3F has no segment: it is skipped, not read with a length.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BareMarkersInTheMainHeaderAreSkipped()
    {
        var options = new JpxTestOptions();
        var planes = JpxDecoderTests.Planes(options);
        var stream = JpxTestEncoder.Encode(options, planes);
        var start = stream.AsSpan().IndexOf(StartOfTile);
        byte[] marked = [.. stream.AsSpan(0, start), .. BareMarker, .. stream.AsSpan(start)];
        var decoded = JpxDecoderTests.DecodePlanes(marked);

        await Assert.That(decoded).IsNotNull();
        await Assert.That(decoded![0].AsSpan().SequenceEqual(planes[0])).IsTrue();
    }

    /// <summary>Builds a PPT segment: the index byte then data bytes.</summary>
    /// <param name="index">The Zppt.</param>
    /// <param name="length">The data bytes.</param>
    /// <returns>The segment.</returns>
    private static byte[] Segment(byte index, int length)
    {
        var segment = new byte[length + 1];
        segment[0] = index;
        return segment;
    }
}
