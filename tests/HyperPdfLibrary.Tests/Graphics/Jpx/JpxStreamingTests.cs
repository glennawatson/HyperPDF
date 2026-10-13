// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jpx;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Graphics.Jpeg;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// The decoder rebuilds an image a block of rows at a time. These tests check that the numbers are exactly those of the
/// whole-tile wavelet, whatever the size, origin, levels and tiling, and that the memory in use does not grow with the
/// image height.
/// </summary>
public sealed class JpxStreamingTests
{
    /// <summary>The components of an RGB image.</summary>
    private const int Rgb = 3;

    /// <summary>The width of the image used to test size-limited decoding.</summary>
    private const int FittingWidth = 200;

    /// <summary>The height of the image used to test size-limited decoding.</summary>
    private const int FittingHeight = 180;

    /// <summary>The wavelet levels of the image used to test size-limited decoding.</summary>
    private const int FittingLevels = 3;

    /// <summary>The quality layers of the tiled images.</summary>
    private const int TwoLayers = 2;

    /// <summary>The index of the HH sub-band within a resolution's detail sub-bands.</summary>
    private const int DiagonalBand = 2;

    /// <summary>The seed of the coefficient generator.</summary>
    private const uint Seed = 4242;

    /// <summary>The magnitude of the 5/3 test coefficients.</summary>
    private const int IntegerRange = 300;

    /// <summary>The magnitude of the 9/7 test coefficients, in sixteenths.</summary>
    private const int FloatRange = 3000;

    /// <summary>The sixteenths in one.</summary>
    private const float Sixteenth = 16F;

    /// <summary>The bits of a sample.</summary>
    private const int SampleBits = 8;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int Bgra = 4;

    /// <summary>The offset of the red byte in a BGRA pixel.</summary>
    private const int Red = 2;

    /// <summary>The DC level shift of eight-bit samples.</summary>
    private const int LevelShift = 128;

    /// <summary>The largest eight-bit sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The width of the tall test images, wide enough that the streaming buffers pass the pool's large-array size.</summary>
    private const int WideWidth = 1200;

    /// <summary>The code-block side of the tall test images, as a power of two.</summary>
    private const int WideBlockExponent = 6;

    /// <summary>The sample value of the flat tall test images.</summary>
    private const int FlatSample = 100;

    /// <summary>The lower of the two heights compared for memory use.</summary>
    private const int ShortHeight = 1024;

    /// <summary>The higher of the two heights compared for memory use.</summary>
    private const int TallHeight = 2048;

    /// <summary>The bytes in an int.</summary>
    private const int IntBytes = 4;

    /// <summary>Rebuilds levels from random sub-bands one block of rows at a time and compares with the whole-tile transform.</summary>
    /// <param name="reversible">Whether the 5/3 filter is used; otherwise the 9/7 filter on float bits.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="x0">The image's horizontal origin.</param>
    /// <param name="y0">The image's vertical origin.</param>
    /// <param name="levels">The wavelet levels.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The test encoder writes an invalid codestream.</exception>
    [Test]
    [Arguments(true, 61, 45, 0, 0, 3)]
    [Arguments(true, 61, 45, 3, 5, 3)]
    [Arguments(true, 100, 130, 0, 0, 4)]
    [Arguments(true, 100, 130, 1, 1, 5)]
    [Arguments(true, 33, 200, 1, 0, 3)]
    [Arguments(true, 257, 97, 0, 1, 3)]
    [Arguments(true, 64, 64, 0, 0, 2)]
    [Arguments(true, 40, 33, 0, 0, 1)]
    [Arguments(true, 1, 50, 0, 0, 2)]
    [Arguments(true, 50, 1, 1, 1, 2)]
    [Arguments(true, 2, 3, 1, 1, 3)]
    [Arguments(true, 20, 20, 0, 0, 0)]
    [Arguments(false, 61, 45, 0, 0, 3)]
    [Arguments(false, 61, 45, 3, 5, 3)]
    [Arguments(false, 100, 130, 0, 0, 4)]
    [Arguments(false, 100, 130, 1, 1, 5)]
    [Arguments(false, 33, 200, 1, 0, 3)]
    [Arguments(false, 257, 97, 0, 1, 3)]
    [Arguments(false, 64, 64, 0, 0, 2)]
    [Arguments(false, 1, 50, 0, 0, 2)]
    [Arguments(false, 50, 1, 1, 1, 2)]
    [Arguments(false, 2, 3, 1, 1, 3)]
    public async Task LevelsMatchTheWholeTileTransform(bool reversible, int width, int height, int x0, int y0, int levels)
    {
        var options = new JpxTestOptions { Width = width, Height = height, X0 = x0, Y0 = y0, Levels = levels };
        var codestream = JpxCodestream.Read(JpxTestEncoder.Encode(options, JpxDecoderTests.Planes(options))) ?? throw new InvalidOperationException("The encoded codestream is valid.");
        using var tile = new JpxTile(1);
        _ = tile.Build(codestream.Geometry, 0, JpxTileParameters.Resolve(codestream.Main, new(1)));
        var component = tile.Components[0] with { Reversible = reversible };
        var coefficients = RandomCoefficients(component.Area.Width * component.Area.Height, reversible);
        var expected = (int[])coefficients.Clone();
        JpxTestWholeTileWavelet.Inverse(tile, component, expected);

        var rows = Rebuild(tile, component, coefficients);

        await Assert.That(rows.AsSpan().SequenceEqual(expected)).IsTrue();
    }

    /// <summary>Each reduced resolution of a tall, offset image is the forward transform's low-pass band at that level.</summary>
    /// <param name="x0">The image's horizontal origin.</param>
    /// <param name="y0">The image's vertical origin.</param>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="levels">The wavelet levels.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">The test encoder writes an invalid codestream.</exception>
    [Test]
    [Arguments(0, 0, 90, 150, 4)]
    [Arguments(3, 5, 90, 150, 4)]
    [Arguments(1, 1, 37, 101, 5)]
    public async Task EveryReducedResolutionMatchesTheForwardTransform(int x0, int y0, int width, int height, int levels)
    {
        var options = new JpxTestOptions { Width = width, Height = height, X0 = x0, Y0 = y0, Levels = levels };
        var planes = JpxDecoderTests.Planes(options);
        var data = JpxTestEncoder.Encode(options, planes);
        var codestream = JpxCodestream.Read(data) ?? throw new InvalidOperationException("The encoded codestream is valid.");
        using var tile = new JpxTile(options.Components);
        _ = tile.Build(codestream.Geometry, 0, JpxTileParameters.Resolve(codestream.Main, new(options.Components)));
        for (var reduction = 0; reduction <= options.Levels; reduction++)
        {
            var transformed = planes[0].Select(static sample => sample - LevelShift).ToArray();
            JpxTestWavelet.Forward(tile, tile.Components[0], transformed, reduction);
            using var decoded = JpxDecoder.Decode(codestream, data, reduction);
            var area = decoded.Areas[0];
            var matches = true;
            for (var y = 0; y < area.Height && matches; y++)
            {
                for (var x = 0; x < area.Width; x++)
                {
                    matches &= decoded.Planes[0][(y * area.Width) + x] == Math.Clamp(transformed[(y * options.Width) + x] + LevelShift, 0, MaxSample);
                }
            }

            await Assert.That(matches).IsTrue();
        }
    }

    /// <summary>Tiled images, whose tile rows start between blocks of rows and differ between subsampled components, decode exactly.</summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="origin">The image's horizontal and vertical origin.</param>
    /// <param name="subsampling">The subsampling of the second and third components.</param>
    /// <param name="tileWidth">The tile width.</param>
    /// <param name="tileHeight">The tile height.</param>
    /// <param name="levels">The wavelet levels.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(150, 210, 0, 1, 70, 83, 3)]
    [Arguments(130, 171, 3, 2, 60, 51, 3)]
    [Arguments(64, 300, 0, 1, 0, 0, 5)]
    [Arguments(97, 129, 1, 1, 33, 65, 4)]
    [Arguments(200, 40, 0, 2, 64, 17, 2)]
    public async Task TiledImagesRoundTrip(int width, int height, int origin, int subsampling, int tileWidth, int tileHeight, int levels)
    {
        var options = new JpxTestOptions
        {
            Width = width,
            Height = height,
            X0 = origin,
            Y0 = origin,
            Components = Rgb,
            ChromaSubsampling = subsampling,
            Transform = subsampling == 1,
            TileWidth = tileWidth,
            TileHeight = tileHeight,
            Levels = levels,
            Layers = TwoLayers,
        };

        await Assert.That(JpxDecoderTests.RoundTrips(options)).IsTrue();
    }

    /// <summary>The image decoder gives the pixels the planes hold for a tiled image whose rows it reads as it converts.</summary>
    /// <param name="width">The image width.</param>
    /// <param name="height">The image height.</param>
    /// <param name="tileWidth">The tile width.</param>
    /// <param name="tileHeight">The tile height.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(90, 120, 40, 50)]
    [Arguments(90, 120, 0, 0)]
    public async Task TiledImageDecodesRowByRow(int width, int height, int tileWidth, int tileHeight)
    {
        var options = new JpxTestOptions { Width = width, Height = height, Components = Rgb, Transform = true, TileWidth = tileWidth, TileHeight = tileHeight };
        var planes = JpxDecoderTests.Planes(options);
        var header = new ImageHeader(options.Width, options.Height, SampleBits, false, false, PdfColorSpace.DeviceRgb, null, null);

        var image = JpxImageDecoder.Decode(header, JpxTestEncoder.Encode(options, planes), 0);

        await Assert.That(image).IsNotNull();
        var matches = true;
        for (var i = 0; i < planes[0].Length; i++)
        {
            matches &= image!.Pixels[(i * Bgra) + Red] == planes[0][i] && image.Pixels[i * Bgra] == planes[Red][i];
        }

        await Assert.That(matches).IsTrue();
    }

    /// <summary>
    /// An image decoded for a caller that can use only a smaller size comes out at the smallest resolution that still
    /// covers that size, and is the same as asking for that many omitted levels; a size larger than the image decodes in full.
    /// </summary>
    /// <param name="maxWidth">The width the caller can use.</param>
    /// <param name="maxHeight">The height the caller can use.</param>
    /// <param name="levels">The levels expected to be left out.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1000, 1000, 0)]
    [Arguments(200, 180, 0)]
    [Arguments(100, 100, 0)]
    [Arguments(100, 90, 1)]
    [Arguments(50, 45, 2)]
    [Arguments(25, 22, 3)]
    [Arguments(1, 1, 4)]
    public async Task FittingDecodeSkipsTheFinestLevels(int maxWidth, int maxHeight, int levels)
    {
        var options = new JpxTestOptions { Width = FittingWidth, Height = FittingHeight, Components = Rgb, Transform = true, Levels = FittingLevels };
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.Width, PdfValue.FromInteger(options.Width));
        dictionary.Add(KnownName.Height, PdfValue.FromInteger(options.Height));
        dictionary.Add(KnownName.Filter, PdfValue.FromName(KnownName.JPXDecode));
        var stream = new PdfStream(dictionary, JpxTestEncoder.Encode(options, JpxDecoderTests.Planes(options)));

        var fitting = PdfImageDecoder.DecodeFitting(stream, maxWidth, maxHeight);
        var expected = PdfImageDecoder.DecodeCompact(stream, levels);

        await Assert.That(fitting).IsNotNull();
        await Assert.That(fitting!.Width).IsEqualTo(expected!.Width);
        await Assert.That(fitting.Height).IsEqualTo(expected.Height);
        await Assert.That(fitting.Pixels.AsSpan().SequenceEqual(expected.Pixels)).IsTrue();
        await Assert.That(fitting.Width).IsGreaterThanOrEqualTo(Math.Min(maxWidth, options.Width));
    }

    /// <summary>The memory a decode keeps does not grow with the image height.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MemoryDoesNotGrowWithHeight()
    {
        var shortKept = RetainedAfterDecode(ShortHeight);
        var tallKept = RetainedAfterDecode(TallHeight);

        await Assert.That(shortKept).IsGreaterThan(0);
        await Assert.That(tallKept).IsEqualTo(shortKept);
        await Assert.That(tallKept).IsLessThan((long)WideWidth * TallHeight * IntBytes);
    }

    /// <summary>Makes random coefficients: integers for the 5/3 filter, floats stored as their bits for the 9/7 filter.</summary>
    /// <param name="count">The number of coefficients.</param>
    /// <param name="reversible">Whether the 5/3 filter is used.</param>
    /// <returns>The coefficients.</returns>
    private static int[] RandomCoefficients(int count, bool reversible)
    {
        var random = new JpegTestRandom(Seed);
        var coefficients = new int[count];
        for (var i = 0; i < count; i++)
        {
            coefficients[i] = reversible
                ? random.Next(-IntegerRange, IntegerRange)
                : BitConverter.SingleToInt32Bits(random.Next(-FloatRange, FloatRange) / Sixteenth);
        }

        return coefficients;
    }

    /// <summary>Rebuilds a tile-component from coefficients laid out as sub-bands, level by level, one row at a time.</summary>
    /// <param name="tile">The tile layout.</param>
    /// <param name="component">The tile-component.</param>
    /// <param name="coefficients">The coefficients, sub-bands in place; they are changed.</param>
    /// <returns>The rebuilt rows, one after another.</returns>
    private static int[] Rebuild(JpxTile tile, in JpxTileComponent component, int[] coefficients)
    {
        using var workers = new JpxBlockWorkers(new(), CancellationToken.None);
        var stride = component.Area.Width;
        var lowest = tile.Resolutions[component.FirstResolution].Area;
        JpxRowSource top = new JpxRegionRows(coefficients, stride, 0, 0, lowest.Width, lowest.Height);
        for (var r = 1; r <= component.Levels; r++)
        {
            var resolution = tile.Resolutions[component.FirstResolution + r];
            var below = tile.Resolutions[component.FirstResolution + r - 1].Area;
            var details = new JpxDetailBands(
                Band(tile, resolution.FirstBand, coefficients, stride),
                Band(tile, resolution.FirstBand + 1, coefficients, stride),
                Band(tile, resolution.FirstBand + DiagonalBand, coefficients, stride));
            var level = new JpxLevelRows();
            level.Bind(workers, JpxLevelShape.Of(resolution.Area, below), component.Reversible, top, details);
            level.Attach(new int[Math.Max(level.RequiredInts, 1)], 0);
            top = level;
        }

        var rows = new int[top.Width * top.Height];
        for (var y = 0; y < top.Height; y++)
        {
            top.NextRow().CopyTo(rows.AsSpan(y * top.Width, top.Width));
        }

        return rows;
    }

    /// <summary>Makes a source for a sub-band that sits in the coefficient buffer.</summary>
    /// <param name="tile">The tile layout.</param>
    /// <param name="band">The sub-band index.</param>
    /// <param name="coefficients">The coefficients.</param>
    /// <param name="stride">The distance between rows of the buffer.</param>
    /// <returns>The rows of the sub-band.</returns>
    private static JpxRegionRows Band(JpxTile tile, int band, int[] coefficients, int stride)
    {
        var layout = tile.Bands[band];
        return new(coefficients, stride, layout.BufferX, layout.BufferY, layout.Area.Width, layout.Area.Height);
    }

    /// <summary>Decodes a flat tall image into a private cache and reports what the cache keeps for reuse.</summary>
    /// <param name="height">The image height.</param>
    /// <returns>The bytes kept.</returns>
    private static long RetainedAfterDecode(int height)
    {
        var options = new JpxTestOptions { Width = WideWidth, Height = height, BlockExponent = WideBlockExponent };
        var plane = new int[WideWidth * height];
        Array.Fill(plane, FlatSample);
        var data = JpxTestEncoder.Encode(options, [plane]);
        var pools = new ScratchPools { Budget = long.MaxValue };
        var header = new ImageHeader(WideWidth, height, SampleBits, false, false, PdfColorSpace.DeviceGray, null, null);

        var image = JpxImageDecoder.Decode(header, data, 0, 0, pools, CancellationToken.None);

        return image is null ? 0 : pools.RetainedBytes;
    }
}
