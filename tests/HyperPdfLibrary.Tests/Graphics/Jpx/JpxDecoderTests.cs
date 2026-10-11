// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jpx;
using HyperPdfLibrary.Tests.Graphics.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// Lossless round trips through the test encoder: the 5/3 path must give back every sample exactly, whatever the
/// progression, mode switches, tiling, precincts or packet layout.
/// </summary>
public sealed class JpxDecoderTests
{
    /// <summary>The block styles combined in one test.</summary>
    internal const JpxBlockStyle AllStyles = JpxBlockStyle.Bypass | JpxBlockStyle.Reset | JpxBlockStyle.TerminateAll | JpxBlockStyle.VerticallyCausal | JpxBlockStyle.SegmentationSymbols;

    /// <summary>The components of an RGB image.</summary>
    private const int Rgb = 3;

    /// <summary>The layers used by the layered tests.</summary>
    private const int ThreeLayers = 3;

    /// <summary>The precinct exponent used by the precinct tests: 16-sample precincts.</summary>
    private const int SmallPrecincts = 4;

    /// <summary>The code-block exponent used with small precincts: 8-sample blocks.</summary>
    private const int SmallBlocks = 3;

    /// <summary>The tile width of the tiled tests.</summary>
    private const int TileWidth = 23;

    /// <summary>The tile height of the tiled tests.</summary>
    private const int TileHeight = 19;

    /// <summary>The odd image origin of the offset tests.</summary>
    private const int OddX0 = 3;

    /// <summary>The odd image origin of the offset tests.</summary>
    private const int OddY0 = 5;

    /// <summary>The chroma subsampling of the subsampled tests.</summary>
    private const int Halved = 2;

    /// <summary>Many decomposition levels for a small image, so some resolutions are one sample or empty.</summary>
    private const int ManyLevels = 7;

    /// <summary>The seed of the sample generator.</summary>
    private const uint Seed = 1234;

    /// <summary>The largest 8-bit sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The DC level shift for eight-bit unsigned samples.</summary>
    private const int LevelShift = 128;

    /// <summary>The weight of the horizontal gradient.</summary>
    private const int GradientX = 3;

    /// <summary>The weight of the vertical gradient.</summary>
    private const int GradientY = 5;

    /// <summary>The weight of the component in the gradient.</summary>
    private const int GradientComponent = 70;

    /// <summary>The spread of the noise added to the gradient.</summary>
    private const int Noise = 40;

    /// <summary>A plain single-component codestream decodes exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainCodestreamRoundTrips() => await Assert.That(RoundTrips(new())).IsTrue();

    /// <summary>Every progression order, with layers and small precincts, decodes exactly.</summary>
    /// <param name="order">The progression order.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments((int)JpxProgressionOrder.LayerResolutionComponentPosition)]
    [Arguments((int)JpxProgressionOrder.ResolutionLayerComponentPosition)]
    [Arguments((int)JpxProgressionOrder.ResolutionPositionComponentLayer)]
    [Arguments((int)JpxProgressionOrder.PositionComponentResolutionLayer)]
    [Arguments((int)JpxProgressionOrder.ComponentPositionResolutionLayer)]
    public async Task ProgressionOrdersRoundTrip(int order)
    {
        var options = new JpxTestOptions { Components = Rgb, Order = (JpxProgressionOrder)order, Layers = ThreeLayers, PrecinctExponent = SmallPrecincts, BlockExponent = SmallBlocks, };

        await Assert.That(RoundTrips(options)).IsTrue();
    }

    /// <summary>Position progressions over subsampled components and an odd origin decode exactly.</summary>
    /// <param name="order">The progression order.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments((int)JpxProgressionOrder.ResolutionPositionComponentLayer)]
    [Arguments((int)JpxProgressionOrder.PositionComponentResolutionLayer)]
    [Arguments((int)JpxProgressionOrder.ComponentPositionResolutionLayer)]
    public async Task PositionOrdersWithSubsamplingRoundTrip(int order)
    {
        var options = new JpxTestOptions
        {
            Components = Rgb,
            ChromaSubsampling = Halved,
            X0 = OddX0,
            Y0 = OddY0,
            Order = (JpxProgressionOrder)order,
            Layers = ThreeLayers,
            PrecinctExponent = SmallPrecincts,
            BlockExponent = SmallBlocks,
        };

        await Assert.That(RoundTrips(options)).IsTrue();
    }

    /// <summary>Each code-block mode switch, and all of them together, decodes exactly.</summary>
    /// <param name="style">The mode switches.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments((int)JpxBlockStyle.Bypass)]
    [Arguments((int)JpxBlockStyle.Reset)]
    [Arguments((int)JpxBlockStyle.TerminateAll)]
    [Arguments((int)JpxBlockStyle.VerticallyCausal)]
    [Arguments((int)JpxBlockStyle.SegmentationSymbols)]
    [Arguments((int)(JpxBlockStyle.Bypass | JpxBlockStyle.TerminateAll))]
    [Arguments((int)AllStyles)]
    public async Task BlockStylesRoundTrip(int style)
    {
        var options = new JpxTestOptions { Style = (JpxBlockStyle)style, Layers = ThreeLayers };

        await Assert.That(RoundTrips(options)).IsTrue();
    }

    /// <summary>Tiles over an odd image origin, with the component transform, decode exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TilesWithOddOriginRoundTrip()
    {
        var options = new JpxTestOptions { Components = Rgb, Transform = true, X0 = OddX0, Y0 = OddY0, TileWidth = TileWidth, TileHeight = TileHeight, };

        await Assert.That(RoundTrips(options)).IsTrue();
    }

    /// <summary>SOP and EPH markers, packed PPT headers and split tile-parts decode exactly.</summary>
    /// <param name="markers">Whether SOP and EPH markers are written.</param>
    /// <param name="packed">Whether the headers go in PPT markers.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task MarkersAndPackedHeadersRoundTrip(bool markers, bool packed)
    {
        var options = new JpxTestOptions
        {
            Components = Rgb,
            Transform = true,
            Layers = ThreeLayers,
            Markers = markers,
            PackedHeaders = packed,
            SplitTileParts = true,
            TileWidth = TileWidth,
            TileHeight = TileHeight,
        };

        await Assert.That(RoundTrips(options)).IsTrue();
    }

    /// <summary>No decomposition, and more levels than the image has samples for, decode exactly.</summary>
    /// <param name="levels">The decomposition levels.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(ManyLevels)]
    public async Task ExtremeLevelsRoundTrip(int levels)
    {
        var options = new JpxTestOptions { Levels = levels, X0 = OddX0, Y0 = OddY0 };

        await Assert.That(RoundTrips(options)).IsTrue();
    }

    /// <summary>POC volumes with different orders, each skipping packets an earlier one sent, decode exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProgressionChangesRoundTrip()
    {
        var options = new JpxTestOptions
        {
            Components = Rgb,
            Layers = ThreeLayers,
            PrecinctExponent = SmallPrecincts,
            BlockExponent = SmallBlocks,
            Changes =
            [
                new(0, 0, Halved, Halved, Rgb, JpxProgressionOrder.LayerResolutionComponentPosition),
                new(1, 1, ThreeLayers, ThreeLayers + 1, Rgb, JpxProgressionOrder.ResolutionPositionComponentLayer),
                new(0, 0, ThreeLayers, ThreeLayers + 1, Rgb, JpxProgressionOrder.ComponentPositionResolutionLayer),
            ],
        };

        await Assert.That(RoundTrips(options)).IsTrue();
    }

    /// <summary>A JP2 file decodes like its bare codestream.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WrappedFileRoundTrips() => await Assert.That(RoundTrips(new() { Components = Rgb, Wrap = true })).IsTrue();

    /// <summary>Reducing to the lowest resolution decodes the original lossless low-pass coefficients.</summary>
    /// <param name="x0">The image's horizontal origin.</param>
    /// <param name="y0">The image's vertical origin.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The test encoder writes an invalid codestream.</exception>
    [Test]
    [Arguments(0, 0)]
    [Arguments(OddX0, OddY0)]
    public async Task LowestResolutionMatchesIndependentForwardWavelet(int x0, int y0)
    {
        var options = new JpxTestOptions { X0 = x0, Y0 = y0 };
        var planes = Planes(options);
        var data = JpxTestEncoder.Encode(options, planes);
        var codestream = JpxCodestream.Read(data) ?? throw new InvalidOperationException("The encoded codestream is valid.");
        using var tile = new JpxTile(options.Components);
        _ = tile.Build(codestream.Geometry, 0, JpxTileParameters.Resolve(codestream.Main, new(options.Components)));

        var coefficients = planes[0].Select(static sample => sample - LevelShift).ToArray();
        JpxTestWavelet.Forward(tile, tile.Components[0], coefficients);
        using var decoded = JpxDecoder.Decode(codestream, data, options.Levels);
        var area = decoded.Areas[0];
        var expected = new int[area.Width * area.Height];
        for (var y = 0; y < area.Height; y++)
        {
            for (var x = 0; x < area.Width; x++)
            {
                expected[(y * area.Width) + x] = Math.Clamp(coefficients[(y * options.Width) + x] + LevelShift, 0, MaxSample);
            }
        }

        await Assert.That(decoded.Planes[0].AsSpan(0, expected.Length).SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Makes deterministic samples for each component: a gradient with noise.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The planes.</returns>
    internal static int[][] Planes(JpxTestOptions options)
    {
        var random = new JpegTestRandom(Seed);
        var planes = new int[options.Components][];
        for (var c = 0; c < options.Components; c++)
        {
            var subsampling = c == 0 ? 1 : options.ChromaSubsampling;
            var width = Ceil(options.X0 + options.Width, subsampling) - Ceil(options.X0, subsampling);
            var height = Ceil(options.Y0 + options.Height, subsampling) - Ceil(options.Y0, subsampling);
            planes[c] = new int[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var value = (x * GradientX) + (y * GradientY) + (c * GradientComponent) + random.Next(0, Noise);
                    planes[c][(y * width) + x] = value % (MaxSample + 1);
                }
            }
        }

        return planes;
    }

    /// <summary>Decodes data with the core decoder.</summary>
    /// <param name="data">A JP2 file or codestream.</param>
    /// <returns>Each component's samples, or <see langword="null"/> when the data is refused.</returns>
    internal static int[][]? DecodePlanes(byte[] data)
    {
        if (JpxFileFormat.Read(data) is not { } file)
        {
            return null;
        }

        var stream = data.AsSpan(file.Codestream.Offset, file.Codestream.Length);
        if (JpxCodestream.Read(stream) is not { } codestream)
        {
            return null;
        }

        using var image = JpxDecoder.Decode(codestream, stream);
        var planes = new int[image.Planes.Length][];
        for (var c = 0; c < planes.Length; c++)
        {
            planes[c] = image.Planes[c].AsSpan(0, image.Areas[c].Width * image.Areas[c].Height).ToArray();
        }

        return planes;
    }

    /// <summary>Divides rounding up.</summary>
    /// <param name="value">The value.</param>
    /// <param name="divisor">The divisor.</param>
    /// <returns>The quotient.</returns>
    private static int Ceil(int value, int divisor) => (value + divisor - 1) / divisor;

    /// <summary>Encodes generated samples and checks the decoder gives them back exactly.</summary>
    /// <param name="options">The options.</param>
    /// <returns><see langword="true"/> when every sample matches.</returns>
    private static bool RoundTrips(JpxTestOptions options)
    {
        var planes = Planes(options);
        var decoded = DecodePlanes(JpxTestEncoder.Encode(options, planes));
        if (decoded is null || decoded.Length != planes.Length)
        {
            return false;
        }

        for (var c = 0; c < planes.Length; c++)
        {
            if (!decoded[c].AsSpan().SequenceEqual(planes[c]))
            {
                return false;
            }
        }

        return true;
    }
}
