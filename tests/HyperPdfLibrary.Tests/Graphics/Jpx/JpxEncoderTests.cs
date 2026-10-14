// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Graphics.Images.Jpx;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>Round trips through the JPEG 2000 encoder and the library's own decoder: a lossless file must give back every sample.</summary>
public sealed class JpxEncoderTests
{
    /// <summary>The width of the default test image.</summary>
    private const int Width = 67;

    /// <summary>The height of the default test image.</summary>
    private const int Height = 53;

    /// <summary>The components of an RGB image.</summary>
    private const int Rgb = 3;

    /// <summary>The components of a CMYK image.</summary>
    private const int Cmyk = 4;

    /// <summary>The bits of an 8-bit sample.</summary>
    private const int Eight = 8;

    /// <summary>The width of the lossy test image.</summary>
    private const int LossyWidth = 160;

    /// <summary>The height of the lossy test image.</summary>
    private const int LossyHeight = 120;

    /// <summary>The largest 8-bit sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The quality of the fidelity checks.</summary>
    private const int HighQuality = 90;

    /// <summary>The signal to noise ratio a high quality file must reach, in decibels.</summary>
    private const double HighQualityDecibels = 38;

    /// <summary>The error floor, so a perfect decode still has a decibel figure.</summary>
    private const double MinimumError = 1e-6;

    /// <summary>The bits of a 16-bit sample.</summary>
    private const int Sixteen = 16;

    /// <summary>The bits of a 12-bit sample.</summary>
    private const int Twelve = 12;

    /// <summary>A sample depth the encoder refuses.</summary>
    private const int TooDeep = 17;

    /// <summary>A code-block side that is not a power of two.</summary>
    private const int NotAPowerOfTwo = 5;

    /// <summary>A channel count with no colour space to declare.</summary>
    private const int TwoChannels = 2;

    /// <summary>The layers of the layered tests.</summary>
    private const int ThreeLayers = 3;

    /// <summary>A tile side that leaves partial tiles on the default image.</summary>
    private const int TileSide = 32;

    /// <summary>A precinct side that gives several precincts on the default image.</summary>
    private const int PrecinctSide = 32;

    /// <summary>A code-block side small enough to give many code-blocks.</summary>
    private const int SmallBlock = 16;

    /// <summary>The bytes of a BGRA pixel.</summary>
    private const int Bgra = 4;

    /// <summary>The alpha byte of a BGRA pixel.</summary>
    private const int AlphaByte = 3;

    /// <summary>The version number of the ICC profile.</summary>
    private const uint IccVersion = 0x0240_0000;

    /// <summary>The colour method of a restricted ICC profile.</summary>
    private const int IccMethod = 2;

    /// <summary>The offset of the class in an ICC profile header.</summary>
    private const int ProfileClassOffset = 12;

    /// <summary>The enumerated colour space of sRGB.</summary>
    private const int SrgbSpace = 16;

    /// <summary>The decibels in a bel's tenth: the factor of the decibel scale.</summary>
    private const double DecibelScale = 10;

    /// <summary>The qualities the trade-off test tries, best first.</summary>
    private static readonly int[] Qualities = [95, 80, 60, 40, 20];

    /// <summary>A grey image decodes exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayRoundTrips() => await Assert.That(RoundTrips(Image(1, Eight), new())).IsTrue();

    /// <summary>An RGB image decodes exactly, with and without the reversible colour transform.</summary>
    /// <param name="transform">Whether the colour transform is used.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task RgbRoundTrips(bool transform) =>
        await Assert.That(RoundTrips(Image(Rgb, Eight), new() { ColorTransform = transform })).IsTrue();

    /// <summary>A CMYK image decodes exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykRoundTrips() =>
        await Assert.That(RoundTrips(Image(Cmyk, Eight) with { ColorSpace = PdfJpxColorSpace.Cmyk }, new())).IsTrue();

    /// <summary>Every sample depth from one to sixteen bits decodes exactly.</summary>
    /// <param name="bits">The bits of a sample.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(4)]
    [Arguments(7)]
    [Arguments(10)]
    [Arguments(12)]
    [Arguments(16)]
    public async Task DepthsRoundTrip(int bits) => await Assert.That(RoundTrips(Image(Rgb, bits), new())).IsTrue();

    /// <summary>A bare codestream decodes exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CodestreamWithoutWrapperRoundTrips() =>
        await Assert.That(RoundTrips(Image(1, Eight), new() { WriteFileFormat = false })).IsTrue();

    /// <summary>Every progression order, with layers and small precincts and code-blocks, decodes exactly.</summary>
    /// <param name="order">The progression order.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfJpxProgression.LayerResolutionComponentPosition)]
    [Arguments(PdfJpxProgression.ResolutionLayerComponentPosition)]
    [Arguments(PdfJpxProgression.ResolutionPositionComponentLayer)]
    [Arguments(PdfJpxProgression.PositionComponentResolutionLayer)]
    [Arguments(PdfJpxProgression.ComponentPositionResolutionLayer)]
    public async Task ProgressionOrdersRoundTrip(PdfJpxProgression order)
    {
        var options = new PdfJpxOptions { Progression = order, Layers = ThreeLayers, PrecinctSize = PrecinctSide, CodeBlockWidth = SmallBlock, CodeBlockHeight = SmallBlock };

        await Assert.That(RoundTrips(Image(Rgb, Eight), options)).IsTrue();
    }

    /// <summary>Each code-block mode switch, and several together, decodes exactly.</summary>
    /// <param name="style">The mode switches.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfJpxBlockStyle.Bypass)]
    [Arguments(PdfJpxBlockStyle.Reset)]
    [Arguments(PdfJpxBlockStyle.TerminateAll)]
    [Arguments(PdfJpxBlockStyle.VerticallyCausal)]
    [Arguments(PdfJpxBlockStyle.SegmentationSymbols)]
    [Arguments(PdfJpxBlockStyle.Bypass | PdfJpxBlockStyle.TerminateAll)]
    [Arguments(PdfJpxBlockStyle.Bypass | PdfJpxBlockStyle.Reset | PdfJpxBlockStyle.TerminateAll | PdfJpxBlockStyle.VerticallyCausal | PdfJpxBlockStyle.SegmentationSymbols)]
    public async Task BlockStylesRoundTrip(PdfJpxBlockStyle style)
    {
        var options = new PdfJpxOptions { BlockStyle = style, Layers = ThreeLayers };

        await Assert.That(RoundTrips(Image(1, Twelve), options)).IsTrue();
    }

    /// <summary>Tiles that leave partial tiles at the edges, packets with SOP and EPH markers, and many layers decode exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TilesAndMarkersRoundTrip()
    {
        var options = new PdfJpxOptions { TileWidth = TileSide, TileHeight = TileSide, Layers = ThreeLayers, StartOfPacketMarkers = true, EndOfPacketHeaderMarkers = true };

        await Assert.That(RoundTrips(Image(Rgb, Eight), options)).IsTrue();
    }

    /// <summary>Odd tile sizes put the wavelet's high-pass samples on different coordinates in different tiles.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OddTileSizesRoundTrip()
    {
        var options = new PdfJpxOptions { TileWidth = TileSide - 1, TileHeight = TileSide + 1 };

        await Assert.That(RoundTrips(Image(Rgb, Eight), options)).IsTrue();
    }

    /// <summary>No decomposition, one level, and more levels than the image has samples for, decode exactly.</summary>
    /// <param name="levels">The decomposition levels.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(8)]
    [Arguments(32)]
    public async Task DecompositionLevelsRoundTrip(int levels) =>
        await Assert.That(RoundTrips(Image(1, Eight), new() { DecompositionLevels = levels })).IsTrue();

    /// <summary>Tiny images and single rows or columns decode exactly.</summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, 1)]
    [Arguments(1, 9)]
    [Arguments(9, 1)]
    [Arguments(2, 2)]
    [Arguments(3, 5)]
    public async Task TinyImagesRoundTrip(int width, int height) =>
        await Assert.That(RoundTrips(JpxEncoderSamples.Gradient(width, height, Rgb, Eight), new())).IsTrue();

    /// <summary>Large code-blocks of different shapes decode exactly.</summary>
    /// <param name="width">The code-block width.</param>
    /// <param name="height">The code-block height.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(4, 4)]
    [Arguments(1024, 4)]
    [Arguments(4, 1024)]
    [Arguments(32, 128)]
    public async Task CodeBlockShapesRoundTrip(int width, int height) =>
        await Assert.That(RoundTrips(Image(1, Eight), new() { CodeBlockWidth = width, CodeBlockHeight = height })).IsTrue();

    /// <summary>A lossy file decodes close to the original, closer at a higher quality, and is smaller at a lower one.</summary>
    /// <param name="components">The channels.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1)]
    [Arguments(Rgb)]
    public async Task LossyQualityTradesSizeForFidelity(int components)
    {
        var image = JpxEncoderSamples.Gradient(LossyWidth, LossyHeight, components, Eight);
        var sizes = new int[Qualities.Length];
        var errors = new double[Qualities.Length];
        for (var i = 0; i < Qualities.Length; i++)
        {
            var file = await PdfJpxEncoder.EncodeAsync(image, new() { Mode = PdfJpxMode.Lossy, Quality = Qualities[i] }, CancellationToken.None);
            sizes[i] = file.Length;
            errors[i] = MeanSquaredError(image, JpxDecoderTests.DecodePlanes(file));
        }

        // Qualities run from high to low: the files shrink and the error grows.
        for (var i = 1; i < Qualities.Length; i++)
        {
            await Assert.That(sizes[i]).IsLessThan(sizes[i - 1]);
            await Assert.That(errors[i]).IsGreaterThan(errors[i - 1]);
        }

        await Assert.That(Decibels(MaxSample, errors[0])).IsGreaterThan(HighQualityDecibels);
        await Assert.That(sizes[^1]).IsLessThan(LossyWidth * LossyHeight * components);
    }

    /// <summary>A lossy file with tiles, layers, precincts and every progression order still decodes close to the original.</summary>
    /// <param name="order">The progression order.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfJpxProgression.LayerResolutionComponentPosition)]
    [Arguments(PdfJpxProgression.ResolutionPositionComponentLayer)]
    [Arguments(PdfJpxProgression.ComponentPositionResolutionLayer)]
    public async Task LossyTilesAndLayersDecode(PdfJpxProgression order)
    {
        var image = Image(Rgb, Eight);
        var options = new PdfJpxOptions
        {
            Mode = PdfJpxMode.Lossy,
            Quality = HighQuality,
            Progression = order,
            Layers = ThreeLayers,
            TileWidth = TileSide,
            TileHeight = TileSide,
            PrecinctSize = PrecinctSide,
        };
        var file = await PdfJpxEncoder.EncodeAsync(image, options, CancellationToken.None);

        await Assert.That(Decibels(MaxSample, MeanSquaredError(image, JpxDecoderTests.DecodePlanes(file)))).IsGreaterThan(HighQualityDecibels);
    }

    /// <summary>Deeper samples quantize in step with their range, so a 12-bit and a 16-bit file are as faithful as an 8-bit one.</summary>
    /// <param name="bits">The bits of a sample.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Twelve)]
    [Arguments(Sixteen)]
    public async Task LossyDeepSamplesDecode(int bits)
    {
        var image = JpxEncoderSamples.Gradient(Width, Height, 1, bits);
        var file = await PdfJpxEncoder.EncodeAsync(image, new() { Mode = PdfJpxMode.Lossy, Quality = HighQuality }, CancellationToken.None);

        await Assert.That(Decibels((1 << bits) - 1, MeanSquaredError(image, JpxDecoderTests.DecodePlanes(file)))).IsGreaterThan(HighQualityDecibels);
    }

    /// <summary>An image with an opacity channel keeps its opacity: the decoder finds the channel through the channel definition box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpacityChannelSurvives()
    {
        var image = Image(Cmyk, Eight) with { HasAlpha = true };
        var planes = JpxDecoderTests.DecodePlanes(PdfJpxEncoder.Encode(image));

        await Assert.That(planes).IsNotNull();
        for (var c = 0; c < Cmyk; c++)
        {
            await Assert.That(planes![c].AsSpan().SequenceEqual(JpxEncoderSamples.Component(image, c))).IsTrue();
        }
    }

    /// <summary>With <c>/SMaskInData 1</c> the decoder takes the encoded opacity channel as the image's alpha.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpacityChannelIsTheSoftMaskInData()
    {
        var image = Image(Cmyk, Eight) with { HasAlpha = true };
        var decoded = JpxImageDecoder.Decode(new(Width, Height, Eight, false, false, null, null, null), PdfJpxEncoder.Encode(image), 1);
        var alpha = JpxEncoderSamples.Component(image, Rgb);

        await Assert.That(decoded).IsNotNull();
        for (var i = 0; i < alpha.Length; i++)
        {
            await Assert.That((int)decoded!.Pixels[(i * Bgra) + AlphaByte]).IsEqualTo(alpha[i]);
        }
    }

    /// <summary>An ICC profile is written as the colour specification, and the decoder reads the colours through it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IccProfileIsTheColourSpecification()
    {
        var profile = IccTestProfileBuilder.Profile(IccVersion, "GRAY", "XYZ ", new IccTestProfileBuilder.Tag("kTRC", IccTestProfileBuilder.GammaCurve(1.0)));
        var gray = Image(1, Eight);

        // The builder leaves the profile class empty; a display profile is one of the classes the restricted method allows.
        "mntr"u8.CopyTo(profile.AsSpan(ProfileClassOffset));
        var file = PdfJpxEncoder.Encode(gray with { IccProfile = profile });
        var header = PdfAJpxReader.Parse(file);
        var plain = JpxImageDecoder.Decode(new(Width, Height, Eight, false, false, null, null, null), PdfJpxEncoder.Encode(gray), 0);
        var viaProfile = JpxImageDecoder.Decode(new(Width, Height, Eight, false, false, null, null, null), file, 0);

        await Assert.That(header.Method).IsEqualTo(IccMethod);
        await Assert.That(header.ColourSpecifications).IsEqualTo(1);
        await Assert.That(viaProfile).IsNotNull();
        await Assert.That(viaProfile!.Pixels.AsSpan().SequenceEqual(plain!.Pixels)).IsFalse();
    }

    /// <summary>The file meets the structure PDF/A asks of JPEG 2000 data: one colour specification, an enumerated space, no bits-per-component box.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FileMeetsThePdfAHeaderRules()
    {
        var header = PdfAJpxReader.Parse(PdfJpxEncoder.Encode(Image(Rgb, Eight)));

        await Assert.That(header.Channels).IsEqualTo(Rgb);
        await Assert.That(header.BitDepth).IsEqualTo(Eight);
        await Assert.That(header.HasBitsPerComponentBox).IsFalse();
        await Assert.That(header.ColourSpecifications).IsEqualTo(1);
        await Assert.That(header.Method).IsEqualTo(1);
        await Assert.That(header.EnumeratedSpace).IsEqualTo(SrgbSpace);
    }

    /// <summary>The JP2 header declares the size, depth, enumerated colour space and the channels of an image with opacity.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HeaderBoxesDescribeTheImage()
    {
        var file = PdfJpxEncoder.Encode(Image(Cmyk, Eight) with { HasAlpha = true });
        var info = JpxFileFormat.Read(file);

        await Assert.That(info).IsNotNull();
        await Assert.That(info!.ColorSpace).IsEqualTo(JpxColorSpace.Srgb);
        await Assert.That(info.Definitions).IsNotNull();
        await Assert.That(info.Definitions!.Length).IsEqualTo(Cmyk);
        await Assert.That(info.Definitions[Rgb].Type).IsEqualTo(1);
    }

    /// <summary>The lossless encoding is the same every time.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EncodingIsDeterministic()
    {
        var image = Image(Rgb, Eight);

        await Assert.That(PdfJpxEncoder.Encode(image).AsSpan().SequenceEqual(PdfJpxEncoder.Encode(image))).IsTrue();
    }

    /// <summary>Bad sizes, options and colour descriptions are refused with the right exception.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BadInputIsRefused()
    {
        var image = Image(1, Eight);

        await Assert.That(() => PdfJpxEncoder.Encode(image with { Width = 0 })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfJpxEncoder.Encode(image with { BitsPerComponent = TooDeep })).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfJpxEncoder.Encode(image with { Samples = new byte[Rgb] })).Throws<ArgumentException>();
        await Assert.That(() => PdfJpxEncoder.Encode(image with { ColorSpace = PdfJpxColorSpace.Srgb })).Throws<ArgumentException>();
        await Assert.That(() => PdfJpxEncoder.Encode(image, new() { CodeBlockWidth = NotAPowerOfTwo }, CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfJpxEncoder.Encode(image, new() { Layers = 0 }, CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => PdfJpxEncoder.Encode(image, new() { PrecinctSize = Rgb }, CancellationToken.None)).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => PdfJpxEncoder.Encode(Image(TwoChannels, Eight))).Throws<ArgumentException>();
    }

    /// <summary>A cancelled token stops the encode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellationStopsTheEncode()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.That(() => PdfJpxEncoder.Encode(Image(1, Eight), new(), source.Token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await PdfJpxEncoder.EncodeAsync(Image(1, Eight), new(), source.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>The stream overload writes the same file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StreamOverloadWritesTheFile()
    {
        var image = Image(Rgb, Eight);
        await using var stream = new MemoryStream();

        await PdfJpxEncoder.EncodeAsync(image, new(), stream, CancellationToken.None);

        await Assert.That(stream.ToArray().AsSpan().SequenceEqual(PdfJpxEncoder.Encode(image))).IsTrue();
    }

    /// <summary>Converts a mean squared error to a peak signal to noise ratio.</summary>
    /// <param name="peak">The largest sample value.</param>
    /// <param name="error">The mean squared error.</param>
    /// <returns>The ratio in decibels.</returns>
    private static double Decibels(int peak, double error) => DecibelScale * Math.Log10((double)peak * peak / error);

    /// <summary>Gets the mean squared error between an image and decoded planes.</summary>
    /// <param name="image">The original.</param>
    /// <param name="planes">The decoded planes.</param>
    /// <returns>The mean squared error over all samples, at least a tiny amount so a decibel figure exists.</returns>
    private static double MeanSquaredError(PdfJpxImage image, int[][]? planes)
    {
        var sum = 0.0;
        for (var c = 0; c < image.Components; c++)
        {
            var original = JpxEncoderSamples.Component(image, c);
            for (var i = 0; i < original.Length; i++)
            {
                var difference = original[i] - planes![c][i];
                sum += (double)difference * difference;
            }
        }

        return Math.Max(sum / (image.Width * image.Height * image.Components), MinimumError);
    }

    /// <summary>Makes the default test image.</summary>
    /// <param name="components">The channels.</param>
    /// <param name="bits">The bits of a sample.</param>
    /// <returns>The image.</returns>
    private static PdfJpxImage Image(int components, int bits) => JpxEncoderSamples.Gradient(Width, Height, components, bits);

    /// <summary>Encodes an image, decodes the file and compares every sample.</summary>
    /// <param name="image">The image.</param>
    /// <param name="options">The options.</param>
    /// <returns><see langword="true"/> when every sample matches.</returns>
    private static bool RoundTrips(PdfJpxImage image, PdfJpxOptions options)
    {
        var planes = JpxDecoderTests.DecodePlanes(PdfJpxEncoder.Encode(image, options, CancellationToken.None));
        if (planes is null || planes.Length != image.Components)
        {
            return false;
        }

        for (var c = 0; c < planes.Length; c++)
        {
            if (!planes[c].AsSpan().SequenceEqual(JpxEncoderSamples.Component(image, c)))
            {
                return false;
            }
        }

        return true;
    }
}
