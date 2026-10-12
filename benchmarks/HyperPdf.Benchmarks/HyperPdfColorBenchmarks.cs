// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Converts a 4 megapixel CMYK image, row by row, through an ICC look-up table profile and through DeviceCMYK. The profile
/// is a 9-point 'mft1' table whose inks darken and tint the Lab output.
/// </summary>
public class HyperPdfColorBenchmarks
{
    /// <summary>The image side in pixels: 2048 by 2048 is about 4.2 megapixels.</summary>
    private const int Side = 2048;

    /// <summary>The components of CMYK.</summary>
    private const int CmykComponents = 4;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The grid points per channel in the profile's table.</summary>
    private const int Grid = 9;

    /// <summary>The size of the profile header.</summary>
    private const int HeaderSize = 128;

    /// <summary>The bytes in a tag table entry.</summary>
    private const int TagEntrySize = 12;

    /// <summary>The offset of the tables in an 'mft1' tag.</summary>
    private const int TableOffset = 48;

    /// <summary>The entries of each 'mft1' curve table.</summary>
    private const int CurveEntries = 256;

    /// <summary>The output channels of the table.</summary>
    private const int OutputChannels = 3;

    /// <summary>The offset of the colour space signature in the header.</summary>
    private const int ColorSpaceOffset = 16;

    /// <summary>The offset of the connection space signature in the header.</summary>
    private const int ConnectionSpaceOffset = 20;

    /// <summary>The offset of the file signature in the header.</summary>
    private const int FileSignatureOffset = 36;

    /// <summary>The offset of the first tag's offset field.</summary>
    private const int TagOffsetField = HeaderSize + 8;

    /// <summary>The offset of the first tag's size field.</summary>
    private const int TagSizeField = HeaderSize + 12;

    /// <summary>The offset of the tag count.</summary>
    private const int TagCountField = HeaderSize;

    /// <summary>The offset of the identity matrix diagonal step in an 'mft1' header.</summary>
    private const int MatrixOffset = 12;

    /// <summary>The s15Fixed16 value 1.0.</summary>
    private const int FixedOne = 65_536;

    /// <summary>The numbers between diagonal entries of a 3 by 3 matrix.</summary>
    private const int DiagonalStep = 4;

    /// <summary>The largest byte.</summary>
    private const double MaxByte = 255;

    /// <summary>The L* range of the model.</summary>
    private const double LabRange = 100;

    /// <summary>The a* offset of the stored model.</summary>
    private const double ChromaOffset = 128;

    /// <summary>The offset of the version in the header.</summary>
    private const int VersionOffset = 8;

    /// <summary>The version number of a v2 profile, 2.4.</summary>
    private const uint Version2 = 0x0240_0000;

    /// <summary>The shift that leaves one bit of jitter in smooth content.</summary>
    private const int SmoothJitterShift = 7;

    /// <summary>The multiplier of the pseudo-random generator.</summary>
    private const uint NoiseMultiplier = 2_654_435_761;

    /// <summary>The shift that takes the high bits of the generator.</summary>
    private const int NoiseShift = 24;

    /// <summary>The weight of cyan, magenta and yellow in lightness.</summary>
    private static readonly double[] InkLightness = [0.4, 0.3, 0.15, 0];

    /// <summary>The weight of each ink in a*.</summary>
    private static readonly double[] InkAlpha = [-55, 60, 15, 0];

    /// <summary>The weight of each ink in b*.</summary>
    private static readonly double[] InkBeta = [-35, -10, 75, 0];

    /// <summary>The CMYK samples of the image.</summary>
    private byte[] _samples = [];

    /// <summary>The BGRA output.</summary>
    private byte[] _bgra = [];

    /// <summary>The profile conversion.</summary>
    private IccTransform _icc = null!;

    /// <summary>The kinds of image content.</summary>
    public enum ImageContent
    {
        /// <summary>Every pixel is different, so no conversion repeats.</summary>
        Noise = 0,

        /// <summary>Neighbouring pixels are equal or nearly so, as in a photograph or a flat fill.</summary>
        Smooth = 1,
    }

    /// <summary>Gets or sets the kind of image content.</summary>
    [Params(ImageContent.Noise, ImageContent.Smooth)]
    public ImageContent Content { get; set; }

    /// <summary>Builds the profile and fills the image.</summary>
    /// <exception cref="InvalidOperationException">The benchmark profile was not accepted.</exception>
    [GlobalSetup]
    public void Setup()
    {
        _icc = IccProfile.Create(BuildProfile()) ?? throw new InvalidOperationException("The benchmark profile was not accepted.");
        _samples = new byte[Side * Side * CmykComponents];
        _bgra = new byte[Side * Side * BytesPerPixel];
        Fill();

        // The first conversion builds the transform's table; keep it out of the measurement.
        ConvertAll(_icc);
    }

    /// <summary>Converts the image with DeviceCMYK, which PDFium's table interpolation defines.</summary>
    /// <returns>The first output byte.</returns>
    [Benchmark(Baseline = true)]
    public byte DeviceCmyk()
    {
        for (var y = 0; y < Side; y++)
        {
            PdfColorSpace.DeviceCmyk.ConvertRow(_samples.AsSpan(y * Side * CmykComponents, Side * CmykComponents), _bgra.AsSpan(y * Side * BytesPerPixel, Side * BytesPerPixel), Side);
        }

        return _bgra[0];
    }

    /// <summary>Converts the image through the ICC look-up table profile.</summary>
    /// <returns>The first output byte.</returns>
    [Benchmark]
    public byte IccLookUp()
    {
        ConvertAll(_icc);
        return _bgra[0];
    }

    /// <summary>Builds a v2 CMYK profile with an 'A2B0' 'mft1' table.</summary>
    /// <returns>The profile bytes.</returns>
    private static byte[] BuildProfile()
    {
        var nodes = 1;
        for (var c = 0; c < CmykComponents; c++)
        {
            nodes *= Grid;
        }

        var tag = new byte[TableOffset + (CmykComponents * CurveEntries) + (nodes * OutputChannels) + (OutputChannels * CurveEntries)];
        "mft1"u8.CopyTo(tag);
        tag[8] = CmykComponents;
        tag[9] = OutputChannels;
        tag[10] = Grid;
        for (var i = 0; i < OutputChannels; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(tag.AsSpan(MatrixOffset + (i * DiagonalStep * sizeof(int))), FixedOne);
        }

        var position = TableOffset;
        for (var c = 0; c < CmykComponents; c++)
        {
            position = WriteRamp(tag, position);
        }

        position = WriteNodes(tag, position);
        for (var c = 0; c < OutputChannels; c++)
        {
            position = WriteRamp(tag, position);
        }

        var profile = new byte[HeaderSize + sizeof(int) + TagEntrySize + tag.Length];
        BinaryPrimitives.WriteUInt32BigEndian(profile, (uint)profile.Length);
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(VersionOffset), Version2);
        "CMYK"u8.CopyTo(profile.AsSpan(ColorSpaceOffset));
        "Lab "u8.CopyTo(profile.AsSpan(ConnectionSpaceOffset));
        "acsp"u8.CopyTo(profile.AsSpan(FileSignatureOffset));
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(TagCountField), 1);
        "A2B0"u8.CopyTo(profile.AsSpan(HeaderSize + sizeof(int)));
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(TagOffsetField), (uint)(HeaderSize + sizeof(int) + TagEntrySize));
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(TagSizeField), (uint)tag.Length);
        tag.CopyTo(profile, HeaderSize + sizeof(int) + TagEntrySize);
        return profile;
    }

    /// <summary>Writes an identity table of bytes.</summary>
    /// <param name="tag">The tag buffer.</param>
    /// <param name="position">The position.</param>
    /// <returns>The position after the table.</returns>
    private static int WriteRamp(byte[] tag, int position)
    {
        for (var i = 0; i < CurveEntries; i++)
        {
            tag[position + i] = (byte)i;
        }

        return position + CurveEntries;
    }

    /// <summary>Writes the table nodes from the ink model, the first channel varying slowest.</summary>
    /// <param name="tag">The tag buffer.</param>
    /// <param name="position">The position.</param>
    /// <returns>The position after the nodes.</returns>
    private static int WriteNodes(byte[] tag, int position)
    {
        Span<double> ink = stackalloc double[CmykComponents];
        var total = 1;
        for (var c = 0; c < CmykComponents; c++)
        {
            total *= Grid;
        }

        for (var n = 0; n < total; n++)
        {
            var rest = n;
            for (var c = CmykComponents - 1; c >= 0; c--)
            {
                ink[c] = (rest % Grid) / (double)(Grid - 1);
                rest /= Grid;
            }

            var lightness = LabRange * (1 - ink[CmykComponents - 1]) * (1 - Dot(InkLightness, ink));
            tag[position] = Stored(lightness / LabRange);
            tag[position + 1] = Stored((Dot(InkAlpha, ink) + ChromaOffset) / MaxByte);
            tag[position + OutputChannels - 1] = Stored((Dot(InkBeta, ink) + ChromaOffset) / MaxByte);
            position += OutputChannels;
        }

        return position;
    }

    /// <summary>Converts a stored fraction to a byte.</summary>
    /// <param name="fraction">The fraction.</param>
    /// <returns>The byte.</returns>
    private static byte Stored(double fraction) => (byte)Math.Round(Math.Clamp(fraction, 0, 1) * MaxByte);

    /// <summary>Gets the sum of products of two arrays.</summary>
    /// <param name="weights">The weights.</param>
    /// <param name="values">The values.</param>
    /// <returns>The dot product.</returns>
    private static double Dot(double[] weights, ReadOnlySpan<double> values)
    {
        var sum = 0.0;
        for (var i = 0; i < weights.Length; i++)
        {
            sum += weights[i] * values[i];
        }

        return sum;
    }

    /// <summary>Converts every row.</summary>
    /// <param name="transform">The conversion.</param>
    private void ConvertAll(IccTransform transform)
    {
        for (var y = 0; y < Side; y++)
        {
            transform.ConvertRow(_samples.AsSpan(y * Side * CmykComponents, Side * CmykComponents), _bgra.AsSpan(y * Side * BytesPerPixel, Side * BytesPerPixel), Side);
        }
    }

    /// <summary>Fills the image with the chosen content.</summary>
    private void Fill()
    {
        var state = 1U;
        for (var i = 0; i < _samples.Length; i++)
        {
            state *= NoiseMultiplier;
            var noise = (byte)(state >> NoiseShift);
            if (Content == ImageContent.Noise)
            {
                _samples[i] = noise;
                continue;
            }

            var pixel = i / CmykComponents;
            var position = ((pixel % Side) + (pixel / Side) + (i % CmykComponents)) * byte.MaxValue / (Side + Side);
            _samples[i] = (byte)(position + (noise >> SmoothJitterShift));
        }
    }
}
