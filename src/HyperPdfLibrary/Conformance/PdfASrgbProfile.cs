// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Builds an ICC version 2 display profile for sRGB from the published colorimetry of IEC 61966-2-1: the Rec. 709
/// primaries and D65 white, adapted to the D50 profile connection space with the Bradford transform, and the sRGB tone
/// curve sampled at 1024 points. Version 2 is the newest version PDF/A-1 accepts (ISO 19005-1 §6.2.2), and the later
/// parts accept it too. No published profile's bytes are copied.
/// </summary>
internal static class PdfASrgbProfile
{
    /// <summary>The name the output intent and the profile description use.</summary>
    internal const string Name = "sRGB IEC61966-2.1";

    /// <summary>The size of the profile header.</summary>
    private const int HeaderSize = 128;

    /// <summary>The size of the tag count field.</summary>
    private const int CountSize = 4;

    /// <summary>The size of one tag table entry.</summary>
    private const int TagEntrySize = 12;

    /// <summary>The number of tags: desc, cprt, wtpt, the three colorants and the three curves.</summary>
    private const int TagCount = 9;

    /// <summary>The number of samples in each tone curve.</summary>
    private const int CurvePoints = 1024;

    /// <summary>The size of a tag's type signature and reserved field.</summary>
    private const int TagPrefix = 8;

    /// <summary>The size of a four-byte field.</summary>
    private const int Field = 4;

    /// <summary>The size of a two-byte field.</summary>
    private const int ShortField = 2;

    /// <summary>The size of an XYZ tag: its prefix and three numbers.</summary>
    private const int XyzTagSize = TagPrefix + (Axes * Field);

    /// <summary>The fixed tail of a version 2 text description: Unicode and ScriptCode fields.</summary>
    private const int DescriptionTail = Field + Field + ShortField + 1 + ScriptCodeFiller;

    /// <summary>The ScriptCode field's fixed buffer.</summary>
    private const int ScriptCodeFiller = 67;

    /// <summary>The profile version: 2.1.0.</summary>
    private const uint Version = 0x02100000;

    /// <summary>The scale of an s15Fixed16Number.</summary>
    private const double FixedScale = 65_536;

    /// <summary>The largest 16-bit curve value.</summary>
    private const double CurveScale = 65_535;

    /// <summary>The year written in the header's creation date.</summary>
    private const ushort Year = 2026;

    /// <summary>The offset of the version in the header.</summary>
    private const int VersionOffset = 8;

    /// <summary>The offset of the device class in the header.</summary>
    private const int ClassOffset = 12;

    /// <summary>The offset of the colour space in the header.</summary>
    private const int SpaceOffset = 16;

    /// <summary>The offset of the connection space in the header.</summary>
    private const int ConnectionOffset = 20;

    /// <summary>The offset of the creation date in the header.</summary>
    private const int DateOffset = 24;

    /// <summary>The offset of the file signature in the header.</summary>
    private const int SignatureOffset = 36;

    /// <summary>The offset of the illuminant in the header.</summary>
    private const int IlluminantOffset = 68;

    /// <summary>The number of axes of an XYZ value and of RGB.</summary>
    private const int Axes = 3;

    /// <summary>The index of the third axis.</summary>
    private const int Third = 2;

    /// <summary>The values per chromaticity: x and y.</summary>
    private const int Pair = 2;

    /// <summary>The start of the third row of a 3 by 3 matrix.</summary>
    private const int TwoAxes = Axes + Axes;

    /// <summary>The number of entries of a 3 by 3 matrix.</summary>
    private const int MatrixSize = Axes * Axes;

    /// <summary>The index of the tag data block the three tone curves share.</summary>
    private const int CurveBlock = 6;

    /// <summary>The sRGB curve's linear segment ends here (IEC 61966-2-1).</summary>
    private const double LinearLimit = 0.04045;

    /// <summary>The slope divisor of the linear segment.</summary>
    private const double LinearSlope = 12.92;

    /// <summary>The offset of the power segment.</summary>
    private const double PowerOffset = 0.055;

    /// <summary>The scale of the power segment.</summary>
    private const double PowerScale = 1.055;

    /// <summary>The exponent of the power segment.</summary>
    private const double Exponent = 2.4;

    /// <summary>The D50 white's X.</summary>
    private const double D50X = 0.9642;

    /// <summary>The D50 white's Z.</summary>
    private const double D50Z = 0.8249;

    /// <summary>The red primary's x chromaticity.</summary>
    private const double RedX = 0.64;

    /// <summary>The red primary's y chromaticity.</summary>
    private const double RedY = 0.33;

    /// <summary>The green primary's x chromaticity.</summary>
    private const double GreenX = 0.30;

    /// <summary>The green primary's y chromaticity.</summary>
    private const double GreenY = 0.60;

    /// <summary>The blue primary's x chromaticity.</summary>
    private const double BlueX = 0.15;

    /// <summary>The blue primary's y chromaticity.</summary>
    private const double BlueY = 0.06;

    /// <summary>The D65 white's x chromaticity.</summary>
    private const double WhiteX = 0.3127;

    /// <summary>The D65 white's y chromaticity.</summary>
    private const double WhiteY = 0.3290;

    /// <summary>The Bradford matrix, row 1 column 1.</summary>
    private const double B11 = 0.8951;

    /// <summary>The Bradford matrix, row 1 column 2.</summary>
    private const double B12 = 0.2664;

    /// <summary>The Bradford matrix, row 1 column 3.</summary>
    private const double B13 = -0.1614;

    /// <summary>The Bradford matrix, row 2 column 1.</summary>
    private const double B21 = -0.7502;

    /// <summary>The Bradford matrix, row 2 column 2.</summary>
    private const double B22 = 1.7135;

    /// <summary>The Bradford matrix, row 2 column 3.</summary>
    private const double B23 = 0.0367;

    /// <summary>The Bradford matrix, row 3 column 1.</summary>
    private const double B31 = 0.0389;

    /// <summary>The Bradford matrix, row 3 column 2.</summary>
    private const double B32 = -0.0685;

    /// <summary>The Bradford matrix, row 3 column 3.</summary>
    private const double B33 = 1.0296;

    /// <summary>The profile, built once.</summary>
    private static readonly byte[] Profile = Build();

    /// <summary>Gets the D50 white of the profile connection space.</summary>
    private static ReadOnlySpan<double> D50 => [D50X, 1, D50Z];

    /// <summary>Gets the Bradford cone response matrix, row by row.</summary>
    private static ReadOnlySpan<double> Bradford => [B11, B12, B13, B21, B22, B23, B31, B32, B33];

    /// <summary>Gets the chromaticities of red, green and blue, as x then y.</summary>
    private static ReadOnlySpan<double> Primaries => [RedX, RedY, GreenX, GreenY, BlueX, BlueY];

    /// <summary>Gets a copy of the profile bytes.</summary>
    /// <returns>The profile.</returns>
    internal static byte[] GetBytes() => [.. Profile];

    /// <summary>Builds the profile.</summary>
    /// <returns>The bytes.</returns>
    private static byte[] Build()
    {
        Span<double> colorants = stackalloc double[MatrixSize];
        ComputeColorants(colorants);
        byte[][] data =
        [
            DescriptionTag(Name), TextTag("No copyright, use freely"), XyzTag(D50), XyzTag(colorants[..Axes]), XyzTag(colorants[Axes..TwoAxes]),
            XyzTag(colorants[TwoAxes..]), CurveTag(),
        ];
        uint[] signatures = [Tag("desc"), Tag("cprt"), Tag("wtpt"), Tag("rXYZ"), Tag("gXYZ"), Tag("bXYZ"), Tag("rTRC"), Tag("gTRC"), Tag("bTRC")];
        var offsets = new int[data.Length];
        var position = HeaderSize + CountSize + (TagCount * TagEntrySize);
        for (var i = 0; i < data.Length; i++)
        {
            offsets[i] = position;
            position = Align(position + data[i].Length);
        }

        var profile = new byte[position];
        WriteHeader(profile);
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(HeaderSize), TagCount);
        for (var i = 0; i < TagCount; i++)
        {
            // The three curves share one block of data, which the profile format allows.
            var block = Math.Min(i, CurveBlock);
            var entry = profile.AsSpan(HeaderSize + CountSize + (i * TagEntrySize));
            BinaryPrimitives.WriteUInt32BigEndian(entry, signatures[i]);
            BinaryPrimitives.WriteUInt32BigEndian(entry[Field..], (uint)offsets[block]);
            BinaryPrimitives.WriteUInt32BigEndian(entry[TagPrefix..], (uint)data[block].Length);
        }

        for (var i = 0; i < data.Length; i++)
        {
            data[i].CopyTo(profile.AsSpan(offsets[i]));
        }

        return profile;
    }

    /// <summary>Writes the 128-byte header.</summary>
    /// <param name="profile">The profile.</param>
    private static void WriteHeader(byte[] profile)
    {
        var header = profile.AsSpan();
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)profile.Length);
        BinaryPrimitives.WriteUInt32BigEndian(header[VersionOffset..], Version);
        BinaryPrimitives.WriteUInt32BigEndian(header[ClassOffset..], Tag("mntr"));
        BinaryPrimitives.WriteUInt32BigEndian(header[SpaceOffset..], Tag("RGB "));
        BinaryPrimitives.WriteUInt32BigEndian(header[ConnectionOffset..], Tag("XYZ "));
        BinaryPrimitives.WriteUInt16BigEndian(header[DateOffset..], Year);
        BinaryPrimitives.WriteUInt16BigEndian(header[(DateOffset + ShortField)..], 1);
        BinaryPrimitives.WriteUInt16BigEndian(header[(DateOffset + Field)..], 1);
        BinaryPrimitives.WriteUInt32BigEndian(header[SignatureOffset..], Tag("acsp"));
        WriteXyz(header[IlluminantOffset..], D50);
    }

    /// <summary>Computes the sRGB colorants adapted to D50, as red, green and blue XYZ triples.</summary>
    /// <param name="colorants">Receives nine values.</param>
    private static void ComputeColorants(Span<double> colorants)
    {
        // The primaries' XYZ with Y = 1 form the columns of M; scaling each column so M times (1, 1, 1) is the white point
        // gives the RGB to XYZ matrix under D65.
        Span<double> m = stackalloc double[MatrixSize];
        for (var c = 0; c < Axes; c++)
        {
            var x = Primaries[c * Pair];
            var y = Primaries[(c * Pair) + 1];
            m[c] = x / y;
            m[Axes + c] = 1;
            m[TwoAxes + c] = (1 - x - y) / y;
        }

        Span<double> white = [WhiteX / WhiteY, 1, (1 - WhiteX - WhiteY) / WhiteY];
        Span<double> inverse = stackalloc double[MatrixSize];
        Invert(m, inverse);
        Span<double> scale = stackalloc double[Axes];
        Multiply(inverse, white, scale);
        Span<double> adapt = stackalloc double[MatrixSize];
        Adaptation(white, adapt);
        Span<double> column = stackalloc double[Axes];
        for (var c = 0; c < Axes; c++)
        {
            for (var r = 0; r < Axes; r++)
            {
                column[r] = m[(r * Axes) + c] * scale[c];
            }

            Multiply(adapt, column, colorants[(c * Axes)..]);
        }
    }

    /// <summary>Builds the Bradford adaptation from a source white to D50.</summary>
    /// <param name="source">The source white.</param>
    /// <param name="adapt">Receives the 3 by 3 matrix.</param>
    private static void Adaptation(ReadOnlySpan<double> source, Span<double> adapt)
    {
        Span<double> sourceCone = stackalloc double[Axes];
        Span<double> targetCone = stackalloc double[Axes];
        Multiply(Bradford, source, sourceCone);
        Multiply(Bradford, D50, targetCone);
        Span<double> inverse = stackalloc double[MatrixSize];
        Invert(Bradford, inverse);
        Span<double> scaled = stackalloc double[MatrixSize];
        for (var i = 0; i < MatrixSize; i++)
        {
            scaled[i] = Bradford[i] * targetCone[i / Axes] / sourceCone[i / Axes];
        }

        for (var i = 0; i < MatrixSize; i++)
        {
            var r = i / Axes;
            var c = i % Axes;
            adapt[i] = (inverse[r * Axes] * scaled[c]) + (inverse[(r * Axes) + 1] * scaled[Axes + c]) + (inverse[(r * Axes) + Third] * scaled[TwoAxes + c]);
        }
    }

    /// <summary>Multiplies a 3 by 3 matrix by a vector.</summary>
    /// <param name="matrix">The matrix, row by row.</param>
    /// <param name="vector">The vector.</param>
    /// <param name="result">Receives the product.</param>
    private static void Multiply(ReadOnlySpan<double> matrix, ReadOnlySpan<double> vector, Span<double> result)
    {
        for (var r = 0; r < Axes; r++)
        {
            result[r] = (matrix[r * Axes] * vector[0]) + (matrix[(r * Axes) + 1] * vector[1]) + (matrix[(r * Axes) + Third] * vector[Third]);
        }
    }

    /// <summary>Inverts a 3 by 3 matrix by its cofactors, which the cyclic index form gives directly.</summary>
    /// <param name="m">The matrix, row by row.</param>
    /// <param name="inverse">Receives the inverse.</param>
    private static void Invert(ReadOnlySpan<double> m, Span<double> inverse)
    {
        Span<double> cofactors = stackalloc double[MatrixSize];
        for (var r = 0; r < Axes; r++)
        {
            for (var c = 0; c < Axes; c++)
            {
                var r1 = (r + 1) % Axes;
                var r2 = (r + Third) % Axes;
                var c1 = (c + 1) % Axes;
                var c2 = (c + Third) % Axes;
                cofactors[(r * Axes) + c] = (m[(r1 * Axes) + c1] * m[(r2 * Axes) + c2]) - (m[(r1 * Axes) + c2] * m[(r2 * Axes) + c1]);
            }
        }

        var determinant = (m[0] * cofactors[0]) + (m[1] * cofactors[1]) + (m[Third] * cofactors[Third]);
        for (var r = 0; r < Axes; r++)
        {
            for (var c = 0; c < Axes; c++)
            {
                inverse[(r * Axes) + c] = cofactors[(c * Axes) + r] / determinant;
            }
        }
    }

    /// <summary>Builds a version 2 text description tag.</summary>
    /// <param name="text">The ASCII text.</param>
    /// <returns>The tag data.</returns>
    private static byte[] DescriptionTag(string text)
    {
        var tag = new byte[TagPrefix + Field + text.Length + 1 + DescriptionTail];
        BinaryPrimitives.WriteUInt32BigEndian(tag, Tag("desc"));
        BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(TagPrefix), (uint)(text.Length + 1));
        for (var i = 0; i < text.Length; i++)
        {
            tag[TagPrefix + Field + i] = (byte)text[i];
        }

        return tag;
    }

    /// <summary>Builds a text tag.</summary>
    /// <param name="text">The ASCII text.</param>
    /// <returns>The tag data.</returns>
    private static byte[] TextTag(string text)
    {
        var tag = new byte[TagPrefix + text.Length + 1];
        BinaryPrimitives.WriteUInt32BigEndian(tag, Tag("text"));
        for (var i = 0; i < text.Length; i++)
        {
            tag[TagPrefix + i] = (byte)text[i];
        }

        return tag;
    }

    /// <summary>Builds an XYZ tag.</summary>
    /// <param name="xyz">The three values.</param>
    /// <returns>The tag data.</returns>
    private static byte[] XyzTag(ReadOnlySpan<double> xyz)
    {
        var tag = new byte[XyzTagSize];
        BinaryPrimitives.WriteUInt32BigEndian(tag, Tag("XYZ "));
        WriteXyz(tag.AsSpan(TagPrefix), xyz);
        return tag;
    }

    /// <summary>Builds the sampled sRGB tone curve tag.</summary>
    /// <returns>The tag data.</returns>
    private static byte[] CurveTag()
    {
        var tag = new byte[TagPrefix + Field + (CurvePoints * ShortField)];
        BinaryPrimitives.WriteUInt32BigEndian(tag, Tag("curv"));
        BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(TagPrefix), CurvePoints);
        for (var i = 0; i < CurvePoints; i++)
        {
            var v = i / (double)(CurvePoints - 1);
            var linear = v <= LinearLimit ? v / LinearSlope : Math.Pow((v + PowerOffset) / PowerScale, Exponent);
            BinaryPrimitives.WriteUInt16BigEndian(tag.AsSpan(TagPrefix + Field + (i * ShortField)), (ushort)Math.Round(linear * CurveScale));
        }

        return tag;
    }

    /// <summary>Writes three s15Fixed16Number values.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="xyz">The values.</param>
    private static void WriteXyz(Span<byte> destination, ReadOnlySpan<double> xyz)
    {
        for (var i = 0; i < Axes; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(destination[(i * Field)..], (int)Math.Round(xyz[i] * FixedScale));
        }
    }

    /// <summary>Reads a four-character signature.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>Its value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Tag(string signature) => BinaryPrimitives.ReadUInt32BigEndian(System.Text.Encoding.ASCII.GetBytes(signature));

    /// <summary>Rounds a position up to the tag alignment.</summary>
    /// <param name="position">The position.</param>
    /// <returns>The aligned position.</returns>
    private static int Align(int position) => (position + Field - 1) / Field * Field;
}
