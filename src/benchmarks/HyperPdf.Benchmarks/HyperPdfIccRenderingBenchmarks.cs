// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Converts colours through ICC profiles for one rendering intent and black point choice: a 1024 by 1024 CMYK image
/// through a look-up table profile, a 1024 by 1024 RGB image through a matrix profile, and a run of CMYK fill colours
/// resolved as <c>scn</c> resolves them. Both profiles have a black lighter than the sRGB black, so compensation changes
/// the conversion.
/// </summary>
public class HyperPdfIccRenderingBenchmarks
{
    /// <summary>The image side in pixels.</summary>
    private const int Side = 1024;

    /// <summary>The components of CMYK.</summary>
    private const int CmykComponents = 4;

    /// <summary>The components of RGB.</summary>
    private const int RgbComponents = 3;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The fill colours resolved per operation.</summary>
    private const int Fills = 4096;

    /// <summary>The grid points per channel in the look-up table.</summary>
    private const int Grid = 9;

    /// <summary>The size of the profile header.</summary>
    private const int HeaderSize = 128;

    /// <summary>The bytes in a tag table entry.</summary>
    private const int TagEntrySize = 12;

    /// <summary>The offset of the tables in an 'mft1' tag.</summary>
    private const int TableOffset = 48;

    /// <summary>The entries of each 'mft1' curve table.</summary>
    private const int CurveEntries = 256;

    /// <summary>The offset of the version in the header.</summary>
    private const int VersionOffset = 8;

    /// <summary>The offset of the colour space signature in the header.</summary>
    private const int ColorSpaceOffset = 16;

    /// <summary>The offset of the connection space signature in the header.</summary>
    private const int ConnectionSpaceOffset = 20;

    /// <summary>The offset of the file signature in the header.</summary>
    private const int FileSignatureOffset = 36;

    /// <summary>The offset of the input channel count in an 'mft1' header; the output channel count follows.</summary>
    private const int ChannelsOffset = 8;

    /// <summary>The offset of the grid point count in an 'mft1' header.</summary>
    private const int GridOffset = 10;

    /// <summary>The entries of the matrix profile's curves.</summary>
    private const int CurveTableEntries = 2;

    /// <summary>The offset of the identity matrix in an 'mft1' header.</summary>
    private const int MatrixOffset = 12;

    /// <summary>The numbers between diagonal entries of a 3 by 3 matrix.</summary>
    private const int DiagonalStep = 4;

    /// <summary>The s15Fixed16 value 1.0.</summary>
    private const int FixedOne = 65_536;

    /// <summary>The version number of a v2 profile, 2.4.</summary>
    private const uint Version2 = 0x0240_0000;

    /// <summary>The bytes of an XYZ tag.</summary>
    private const int XyzTagSize = 20;

    /// <summary>The offset of the numbers in an XYZ tag.</summary>
    private const int XyzOffset = 8;

    /// <summary>The bytes of the two-entry curve tag.</summary>
    private const int CurveTagSize = 16;

    /// <summary>The offset of the entry count in a curve tag.</summary>
    private const int CurveCountOffset = 8;

    /// <summary>The offset of the first entry in a curve tag.</summary>
    private const int CurveDataOffset = 12;

    /// <summary>The 16-bit curve value of a tenth, the lifted black of the matrix profile's curves.</summary>
    private const ushort LiftedBlack = 6554;

    /// <summary>The stored L* of unprinted paper in the look-up table, as a fraction of 100.</summary>
    private const double Paper = 0.95;

    /// <summary>How much each of cyan, magenta and yellow darkens the table, as a fraction of 100 L*.</summary>
    private const double InkDrop = 0.2;

    /// <summary>How much black ink darkens the table, as a fraction of 100 L*.</summary>
    private const double BlackDrop = 0.15;

    /// <summary>How far cyan moves a* and yellow moves b*, as a fraction of the range.</summary>
    private const double Tint = 0.15;

    /// <summary>The stored value of a zero a* or b*.</summary>
    private const double NeutralChroma = 128.0 / 255.0;

    /// <summary>The largest byte.</summary>
    private const double MaxByte = 255;

    /// <summary>The multiplier of the pseudo-random generator.</summary>
    private const uint NoiseMultiplier = 2_654_435_761;

    /// <summary>The shift that takes the high bits of the generator.</summary>
    private const int NoiseShift = 24;

    /// <summary>The sRGB colorants in D50 XYZ, red, green then blue.</summary>
    private static readonly double[] Colorants = [0.4360747, 0.2225045, 0.0139322, 0.3850649, 0.7168786, 0.0971045, 0.1430804, 0.0606169, 0.7141733];

    /// <summary>The CMYK image.</summary>
    private byte[] _cmyk = [];

    /// <summary>The RGB image.</summary>
    private byte[] _rgb = [];

    /// <summary>The BGRA output.</summary>
    private byte[] _bgra = [];

    /// <summary>The fill colours, four components each.</summary>
    private float[] _fills = [];

    /// <summary>The look-up profile space as parsed.</summary>
    private PdfColorSpace _lookUpSpace = null!;

    /// <summary>The look-up profile space for the rendering.</summary>
    private PdfColorSpace _lookUp = null!;

    /// <summary>The matrix profile space for the rendering.</summary>
    private PdfColorSpace _matrix = null!;

    /// <summary>The rendering under test.</summary>
    private IccRendering _rendering;

    /// <summary>The renderings measured.</summary>
    public enum Rendering
    {
        /// <summary>Relative colorimetric with compensation: the initial graphics state and /Default.</summary>
        RelativeCompensated = 0,

        /// <summary>Relative colorimetric with /UseBlackPtComp /OFF.</summary>
        RelativePlain = 1,

        /// <summary>Perceptual with compensation.</summary>
        PerceptualCompensated = 2,

        /// <summary>Perceptual with /UseBlackPtComp /OFF.</summary>
        PerceptualPlain = 3,

        /// <summary>Saturation with compensation.</summary>
        SaturationCompensated = 4,

        /// <summary>Absolute colorimetric, which never compensates.</summary>
        AbsoluteColorimetric = 5,
    }

    /// <summary>Gets or sets the rendering.</summary>
    [Params(Rendering.RelativeCompensated, Rendering.RelativePlain, Rendering.PerceptualCompensated, Rendering.PerceptualPlain, Rendering.SaturationCompensated, Rendering.AbsoluteColorimetric)]
    public Rendering Choice { get; set; }

    /// <summary>Builds the profiles and images and warms the conversions.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _rendering = Choice switch
        {
            Rendering.RelativePlain => new(IccIntent.RelativeColorimetric, false),
            Rendering.PerceptualCompensated => new(IccIntent.Perceptual, true),
            Rendering.PerceptualPlain => new(IccIntent.Perceptual, false),
            Rendering.SaturationCompensated => new(IccIntent.Saturation, true),
            Rendering.AbsoluteColorimetric => IccRendering.From(IccIntent.AbsoluteColorimetric, BlackPointCompensationMode.On),
            _ => IccRendering.Default,
        };
        _lookUpSpace = Parse(BuildLookUpProfile(), CmykComponents);
        _lookUp = _lookUpSpace.ForRendering(_rendering);
        _matrix = Parse(BuildMatrixProfile(), RgbComponents).ForRendering(_rendering);
        _cmyk = Noise(Side * Side * CmykComponents);
        _rgb = Noise(Side * Side * RgbComponents);
        _bgra = new byte[Side * Side * BytesPerPixel];
        _fills = new float[Fills * CmykComponents];
        var bytes = Noise(_fills.Length);
        for (var i = 0; i < _fills.Length; i++)
        {
            _fills[i] = bytes[i] / (float)MaxByte;
        }

        // The first conversions build each transform's tables; keep them out of the measurement.
        _ = LookUpRows();
        _ = MatrixRows();
        _ = ResolveFills();
    }

    /// <summary>Converts the CMYK image row by row through the look-up profile.</summary>
    /// <returns>The first output byte.</returns>
    [Benchmark(Baseline = true)]
    public byte LookUpRows()
    {
        for (var y = 0; y < Side; y++)
        {
            _lookUp.ConvertRow(_cmyk.AsSpan(y * Side * CmykComponents, Side * CmykComponents), _bgra.AsSpan(y * Side * BytesPerPixel, Side * BytesPerPixel), Side);
        }

        return _bgra[0];
    }

    /// <summary>Converts the RGB image row by row through the matrix profile.</summary>
    /// <returns>The first output byte.</returns>
    [Benchmark]
    public byte MatrixRows()
    {
        for (var y = 0; y < Side; y++)
        {
            _matrix.ConvertRow(_rgb.AsSpan(y * Side * RgbComponents, Side * RgbComponents), _bgra.AsSpan(y * Side * BytesPerPixel, Side * BytesPerPixel), Side);
        }

        return _bgra[0];
    }

    /// <summary>Resolves the fill colours in the parsed look-up space for the rendering, as <c>scn</c> does.</summary>
    /// <returns>A combination of the colours.</returns>
    [Benchmark]
    public uint ResolveFills()
    {
        var combined = 0U;
        var fills = _fills.AsSpan();
        for (var i = 0; i < Fills; i++)
        {
            combined ^= ColorState.Resolve(_lookUpSpace, fills.Slice(i * CmykComponents, CmykComponents), _rendering).Rgb;
        }

        return combined;
    }

    /// <summary>Builds a v2 CMYK profile whose 'A2B0' table darkens Lab with each ink, down to L* 20 for the rich black.</summary>
    /// <returns>The profile bytes.</returns>
    internal static byte[] BuildLookUpProfile()
    {
        var nodes = 1;
        for (var c = 0; c < CmykComponents; c++)
        {
            nodes *= Grid;
        }

        var tag = new byte[TableOffset + (CmykComponents * CurveEntries) + (nodes * RgbComponents) + (RgbComponents * CurveEntries)];
        "mft1"u8.CopyTo(tag);
        tag[ChannelsOffset] = CmykComponents;
        tag[ChannelsOffset + 1] = RgbComponents;
        tag[GridOffset] = Grid;
        for (var i = 0; i < RgbComponents; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(tag.AsSpan(MatrixOffset + (i * DiagonalStep * sizeof(int))), FixedOne);
        }

        var position = TableOffset;
        for (var c = 0; c < CmykComponents; c++)
        {
            position = WriteRamp(tag, position);
        }

        position = WriteNodes(tag, position, nodes);
        for (var c = 0; c < RgbComponents; c++)
        {
            position = WriteRamp(tag, position);
        }

        return Assemble("CMYK", "Lab ", [new("A2B0", tag)]);
    }

    /// <summary>Parses an ICCBased space.</summary>
    /// <param name="profile">The profile bytes.</param>
    /// <param name="components">The /N value.</param>
    /// <returns>The space.</returns>
    private static PdfColorSpace Parse(byte[] profile, int components)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Set(KnownName.N, PdfValue.FromInteger(components));
        var stream = new PdfStream(dictionary, profile);
        return PdfColorSpace.Parse(PdfValue.FromArray(new(null, [PdfValue.FromName(KnownName.ICCBased), PdfValue.FromStream(stream)])), null);
    }

    /// <summary>Fills bytes with pseudo-random values, so no pixel repeats its neighbour.</summary>
    /// <param name="length">The byte count.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Noise(int length)
    {
        var bytes = new byte[length];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)(((uint)(i + 1) * NoiseMultiplier) >> NoiseShift);
        }

        return bytes;
    }

    /// <summary>Builds a v2 RGB matrix profile with the sRGB colorants and linear curves that start at a tenth.</summary>
    /// <returns>The profile bytes.</returns>
    private static byte[] BuildMatrixProfile()
    {
        var curve = new byte[CurveTagSize];
        "curv"u8.CopyTo(curve);
        BinaryPrimitives.WriteUInt32BigEndian(curve.AsSpan(CurveCountOffset), CurveTableEntries);
        BinaryPrimitives.WriteUInt16BigEndian(curve.AsSpan(CurveDataOffset), LiftedBlack);
        BinaryPrimitives.WriteUInt16BigEndian(curve.AsSpan(CurveDataOffset + sizeof(ushort)), ushort.MaxValue);
        return Assemble(
            "RGB ",
            "XYZ ",
            [new("rXYZ", Xyz(0)), new("gXYZ", Xyz(RgbComponents)), new("bXYZ", Xyz(RgbComponents + RgbComponents)), new("rTRC", curve), new("gTRC", curve), new("bTRC", curve)]);
    }

    /// <summary>Builds an XYZ tag from three of the colorant numbers.</summary>
    /// <param name="first">The index of the colorant's X.</param>
    /// <returns>The tag data.</returns>
    private static byte[] Xyz(int first)
    {
        var tag = new byte[XyzTagSize];
        "XYZ "u8.CopyTo(tag);
        for (var i = 0; i < RgbComponents; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(tag.AsSpan(XyzOffset + (i * sizeof(int))), (int)Math.Round(Colorants[first + i] * FixedOne));
        }

        return tag;
    }

    /// <summary>Writes a profile header, tag table and tags.</summary>
    /// <param name="colorSpace">The data colour space signature.</param>
    /// <param name="connectionSpace">The connection space signature.</param>
    /// <param name="tags">The tags.</param>
    /// <returns>The profile bytes.</returns>
    private static byte[] Assemble(string colorSpace, string connectionSpace, ProfileTag[] tags)
    {
        var offset = HeaderSize + sizeof(int) + (tags.Length * TagEntrySize);
        var total = offset;
        foreach (var tag in tags)
        {
            total += (tag.Data.Length + sizeof(int) - 1) / sizeof(int) * sizeof(int);
        }

        var profile = new byte[total];
        BinaryPrimitives.WriteUInt32BigEndian(profile, (uint)total);
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(VersionOffset), Version2);
        System.Text.Encoding.ASCII.GetBytes(colorSpace).CopyTo(profile, ColorSpaceOffset);
        System.Text.Encoding.ASCII.GetBytes(connectionSpace).CopyTo(profile, ConnectionSpaceOffset);
        "acsp"u8.CopyTo(profile.AsSpan(FileSignatureOffset));
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(HeaderSize), (uint)tags.Length);
        for (var i = 0; i < tags.Length; i++)
        {
            var entry = HeaderSize + sizeof(int) + (i * TagEntrySize);
            System.Text.Encoding.ASCII.GetBytes(tags[i].Signature).CopyTo(profile, entry);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(entry + sizeof(int)), (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(entry + sizeof(int) + sizeof(int)), (uint)tags[i].Data.Length);
            tags[i].Data.CopyTo(profile, offset);
            offset += (tags[i].Data.Length + sizeof(int) - 1) / sizeof(int) * sizeof(int);
        }

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

    /// <summary>Writes the table nodes, the first channel varying slowest.</summary>
    /// <param name="tag">The tag buffer.</param>
    /// <param name="position">The position.</param>
    /// <param name="nodes">The number of nodes.</param>
    /// <returns>The position after the nodes.</returns>
    private static int WriteNodes(byte[] tag, int position, int nodes)
    {
        Span<double> ink = stackalloc double[CmykComponents];
        for (var n = 0; n < nodes; n++)
        {
            var rest = n;
            for (var c = CmykComponents - 1; c >= 0; c--)
            {
                ink[c] = (rest % Grid) / (double)(Grid - 1);
                rest /= Grid;
            }

            var lightness = Paper - (InkDrop * (ink[0] + ink[1] + ink[RgbComponents - 1])) - (BlackDrop * ink[RgbComponents]);
            tag[position] = Stored(lightness);
            tag[position + 1] = Stored(NeutralChroma - (Tint * ink[0]));
            tag[position + RgbComponents - 1] = Stored(NeutralChroma + (Tint * ink[RgbComponents - 1]));
            position += RgbComponents;
        }

        return position;
    }

    /// <summary>Converts a stored fraction to a byte.</summary>
    /// <param name="fraction">The fraction.</param>
    /// <returns>The byte.</returns>
    private static byte Stored(double fraction) => (byte)Math.Round(Math.Clamp(fraction, 0, 1) * MaxByte);

    /// <summary>A profile tag.</summary>
    /// <param name="Signature">The four-letter signature.</param>
    /// <param name="Data">The tag data.</param>
    private readonly record struct ProfileTag(string Signature, byte[] Data);
}
