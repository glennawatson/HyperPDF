// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Text;

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>Writes small ICC profiles with look-up tables, for tests.</summary>
internal static class IccTestProfileBuilder
{
    /// <summary>The size of the profile header.</summary>
    internal const int HeaderSize = 128;

    /// <summary>The bytes in a tag table entry.</summary>
    internal const int TagEntrySize = 12;

    /// <summary>The offset of the size within a tag table entry.</summary>
    internal const int TagSizeField = 8;

    /// <summary>The offset of the version in the header.</summary>
    internal const int VersionOffset = 8;

    /// <summary>The offset of the colour space signature in the header.</summary>
    internal const int ColorSpaceOffset = 16;

    /// <summary>The offset of the connection space signature in the header.</summary>
    internal const int ConnectionSpaceOffset = 20;

    /// <summary>The offset of the file signature in the header.</summary>
    internal const int FileSignatureOffset = 36;

    /// <summary>The bytes of a 32-bit number.</summary>
    internal const int Number32Size = 4;

    /// <summary>The bytes before the data of a curve tag.</summary>
    internal const int CurveHeader = 12;

    /// <summary>The offset of the grid point count in an 'mft1' or 'mft2' tag.</summary>
    internal const int GridPointsOffset = 10;

    /// <summary>The offset of the table offset in an 'mAB ' tag.</summary>
    internal const int MabClutOffset = 24;

    /// <summary>The version number of a v2 profile, 2.4.</summary>
    internal const uint Version2 = 0x0240_0000;

    /// <summary>The version number of a v4 profile, 4.2.</summary>
    internal const uint Version4 = 0x0420_0000;

    /// <summary>The scale of an s15Fixed16 number.</summary>
    private const double FixedScale = 65_536;

    /// <summary>The alignment of tag data.</summary>
    private const int TagAlignment = 4;

    /// <summary>The offset of the entry count in a 'curv' tag.</summary>
    private const int CurveCountOffset = 8;

    /// <summary>The offset of the input channel count in a table tag; the output channel count follows.</summary>
    private const int ChannelsOffset = 8;

    /// <summary>The offset of the matrix in an 'mft1' or 'mft2' tag.</summary>
    private const int LutMatrixOffset = 12;

    /// <summary>The bytes of the matrix in an 'mft1' or 'mft2' tag.</summary>
    private const int LutMatrixSize = 36;

    /// <summary>The offset of the tables in an 'mft1' tag.</summary>
    private const int Lut8Data = 48;

    /// <summary>The offset of the tables in an 'mft2' tag.</summary>
    private const int Lut16Data = 52;

    /// <summary>The entries of each table in an 'mft1' tag.</summary>
    private const int Lut8Entries = 256;

    /// <summary>The entries of each table in the 'mft2' tags the builder writes.</summary>
    private const int Lut16Entries = 2;

    /// <summary>The bytes in the header of an 'mAB ' tag.</summary>
    private const int MabHeaderSize = 32;

    /// <summary>The offset of the first stage offset in an 'mAB ' tag.</summary>
    private const int MabOffsetsStart = 12;

    /// <summary>The bytes of grid counts in the table of an 'mAB ' tag.</summary>
    private const int MabGridBytes = 16;

    /// <summary>The bytes of the table header of an 'mAB ' tag.</summary>
    private const int MabClutHeader = 20;

    /// <summary>The stages that have an offset in an 'mAB ' tag.</summary>
    private const int MabStageCount = 5;

    /// <summary>The largest 8-bit sample.</summary>
    private const double Maximum8 = 255;

    /// <summary>The largest 16-bit sample.</summary>
    private const double Maximum16 = 65_535;

    /// <summary>The bytes in a 16-bit sample.</summary>
    private const int WideBytes = 2;

    /// <summary>The output channels of every table.</summary>
    private const int OutputChannels = 3;

    /// <summary>The distance between diagonal entries of a 3 by 3 matrix, in numbers.</summary>
    private const int DiagonalStep = OutputChannels + 1;

    /// <summary>The u8Fixed8 scale of a gamma.</summary>
    private const double GammaScale = 256;

    /// <summary>Builds a profile from tags.</summary>
    /// <param name="version">The version number.</param>
    /// <param name="colorSpace">The data colour space signature.</param>
    /// <param name="connectionSpace">The connection space signature.</param>
    /// <param name="tags">The tags.</param>
    /// <returns>The profile bytes.</returns>
    internal static byte[] Profile(uint version, string colorSpace, string connectionSpace, params Tag[] tags)
    {
        var tableEnd = HeaderSize + Number32Size + (tags.Length * TagEntrySize);
        var offset = tableEnd;
        var total = tableEnd;
        foreach (var tag in tags)
        {
            total += Aligned(tag.Data.Length);
        }

        var profile = new byte[total];
        BinaryPrimitives.WriteUInt32BigEndian(profile, (uint)total);
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(VersionOffset), version);
        Encoding.ASCII.GetBytes(colorSpace).CopyTo(profile, ColorSpaceOffset);
        Encoding.ASCII.GetBytes(connectionSpace).CopyTo(profile, ConnectionSpaceOffset);
        "acsp"u8.CopyTo(profile.AsSpan(FileSignatureOffset));
        BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(HeaderSize), (uint)tags.Length);
        for (var i = 0; i < tags.Length; i++)
        {
            var entry = HeaderSize + Number32Size + (i * TagEntrySize);
            Encoding.ASCII.GetBytes(tags[i].Signature).CopyTo(profile, entry);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(entry + Number32Size), (uint)offset);
            BinaryPrimitives.WriteUInt32BigEndian(profile.AsSpan(entry + TagSizeField), (uint)tags[i].Data.Length);
            tags[i].Data.CopyTo(profile, offset);
            offset += Aligned(tags[i].Data.Length);
        }

        return profile;
    }

    /// <summary>Builds an 'mft1' tag whose tables are the identity.</summary>
    /// <param name="inputs">The input channels.</param>
    /// <param name="grid">The grid points per channel.</param>
    /// <param name="map">Maps input positions from 0 to 1 to the three stored output values from 0 to 1.</param>
    /// <returns>The tag data.</returns>
    internal static byte[] Lut8(int inputs, int grid, Func<double[], double[]> map)
    {
        var table = new byte[Lut8Data + (inputs * Lut8Entries) + (Pow(grid, inputs) * OutputChannels) + (OutputChannels * Lut8Entries)];
        WriteLutHeader(table, "mft1", inputs, grid);
        var position = Lut8Data;
        for (var c = 0; c < inputs; c++)
        {
            position = WriteRamp(table, position);
        }

        foreach (var value in Samples(inputs, grid, map))
        {
            table[position] = (byte)Math.Round(value * Maximum8);
            position++;
        }

        for (var c = 0; c < OutputChannels; c++)
        {
            position = WriteRamp(table, position);
        }

        return table;
    }

    /// <summary>Builds an 'mft2' tag whose tables are the identity with two entries.</summary>
    /// <param name="inputs">The input channels.</param>
    /// <param name="grid">The grid points per channel.</param>
    /// <param name="map">Maps input positions from 0 to 1 to the three stored output values from 0 to 1.</param>
    /// <returns>The tag data.</returns>
    internal static byte[] Lut16(int inputs, int grid, Func<double[], double[]> map)
    {
        var table = new byte[Lut16Data + (inputs * Lut16Entries * WideBytes) + (Pow(grid, inputs) * OutputChannels * WideBytes) + (OutputChannels * Lut16Entries * WideBytes)];
        WriteLutHeader(table, "mft2", inputs, grid);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(Lut8Data), Lut16Entries);
        BinaryPrimitives.WriteUInt16BigEndian(table.AsSpan(Lut8Data + WideBytes), Lut16Entries);
        var position = Lut16Data;
        for (var c = 0; c < inputs; c++)
        {
            position = WriteWide(table, position, 0);
            position = WriteWide(table, position, Maximum16);
        }

        foreach (var value in Samples(inputs, grid, map))
        {
            position = WriteWide(table, position, value * Maximum16);
        }

        for (var c = 0; c < OutputChannels; c++)
        {
            position = WriteWide(table, position, 0);
            position = WriteWide(table, position, Maximum16);
        }

        return table;
    }

    /// <summary>Builds an 'mAB ' tag.</summary>
    /// <param name="inputs">The input channels.</param>
    /// <param name="grids">The grid points of each input channel, or <see langword="null"/> for a tag without a table.</param>
    /// <param name="map">Maps input positions to the three stored output values; unused without a table.</param>
    /// <param name="stages">The curves and matrix to include.</param>
    /// <returns>The tag data.</returns>
    internal static byte[] AToB(int inputs, int[]? grids, Func<double[], double[]> map, in MabStages stages)
    {
        var body = new List<byte>();
        var offsets = new uint[MabStageCount];
        offsets[0] = Place(body, Concatenate(stages.OutputCurves));
        offsets[1] = stages.Matrix is null ? 0 : Place(body, MatrixBytes(stages.Matrix));
        offsets[2] = stages.MatrixCurves is null ? 0 : Place(body, Concatenate(stages.MatrixCurves));
        offsets[3] = grids is null ? 0 : Place(body, ClutBytes(grids, map, stages.Precision));
        offsets[4] = stages.InputCurves is null ? 0 : Place(body, Concatenate(stages.InputCurves));

        var tag = new byte[MabHeaderSize + body.Count];
        "mAB "u8.CopyTo(tag);
        tag[ChannelsOffset] = (byte)inputs;
        tag[ChannelsOffset + 1] = OutputChannels;
        for (var i = 0; i < offsets.Length; i++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(MabOffsetsStart + (i * Number32Size)), offsets[i]);
        }

        body.CopyTo(tag, MabHeaderSize);
        return tag;
    }

    /// <summary>Builds a 'curv' tag holding a table.</summary>
    /// <param name="values">The table values from 0 to 1; empty for the identity.</param>
    /// <returns>The tag data.</returns>
    internal static byte[] TableCurve(params double[] values)
    {
        var tag = new byte[CurveHeader + (values.Length * WideBytes)];
        "curv"u8.CopyTo(tag);
        BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(CurveCountOffset), (uint)values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            _ = WriteWide(tag, CurveHeader + (i * WideBytes), values[i] * Maximum16);
        }

        return tag;
    }

    /// <summary>Builds a 'curv' tag holding one gamma.</summary>
    /// <param name="gamma">The gamma.</param>
    /// <returns>The tag data.</returns>
    internal static byte[] GammaCurve(double gamma)
    {
        var tag = new byte[CurveHeader + WideBytes];
        "curv"u8.CopyTo(tag);
        BinaryPrimitives.WriteUInt32BigEndian(tag.AsSpan(CurveCountOffset), 1);
        BinaryPrimitives.WriteUInt16BigEndian(tag.AsSpan(CurveHeader), (ushort)Math.Round(gamma * GammaScale));
        return tag;
    }

    /// <summary>Gets the stored values of a table in file order, the first channel varying slowest.</summary>
    /// <param name="inputs">The input channels.</param>
    /// <param name="grid">The grid points per channel.</param>
    /// <param name="map">Maps input positions to stored values.</param>
    /// <returns>The values, three per node.</returns>
    internal static IEnumerable<double> Samples(int inputs, int grid, Func<double[], double[]> map) =>
        Samples(Enumerable.Repeat(grid, inputs).ToArray(), map);

    /// <summary>Gets the stored values of a table in file order, the first channel varying slowest.</summary>
    /// <param name="grids">The grid points of each channel.</param>
    /// <param name="map">Maps input positions to stored values.</param>
    /// <returns>The values, three per node.</returns>
    internal static IEnumerable<double> Samples(int[] grids, Func<double[], double[]> map)
    {
        var index = new int[grids.Length];
        var nodes = grids.Aggregate(1, static (a, b) => a * b);
        for (var n = 0; n < nodes; n++)
        {
            var position = new double[grids.Length];
            for (var c = 0; c < grids.Length; c++)
            {
                position[c] = index[c] / (double)(grids[c] - 1);
            }

            foreach (var value in map(position))
            {
                yield return Math.Clamp(value, 0, 1);
            }

            for (var c = grids.Length - 1; c >= 0; c--)
            {
                index[c]++;
                if (index[c] < grids[c])
                {
                    break;
                }

                index[c] = 0;
            }
        }
    }

    /// <summary>Appends a section to the body of an 'mAB ' tag, padded to four bytes.</summary>
    /// <param name="body">The body so far.</param>
    /// <param name="data">The section.</param>
    /// <returns>The offset of the section from the start of the tag.</returns>
    private static uint Place(List<byte> body, byte[] data)
    {
        var start = (uint)(MabHeaderSize + body.Count);
        body.AddRange(data);
        while (body.Count % TagAlignment != 0)
        {
            body.Add(0);
        }

        return start;
    }

    /// <summary>Writes a 256-entry identity table of bytes.</summary>
    /// <param name="table">The buffer.</param>
    /// <param name="position">The position.</param>
    /// <returns>The position after the table.</returns>
    private static int WriteRamp(byte[] table, int position)
    {
        for (var i = 0; i < Lut8Entries; i++)
        {
            table[position + i] = (byte)i;
        }

        return position + Lut8Entries;
    }

    /// <summary>Writes the common header of an 'mft1' or 'mft2' tag.</summary>
    /// <param name="table">The tag buffer.</param>
    /// <param name="signature">The type signature.</param>
    /// <param name="inputs">The input channels.</param>
    /// <param name="grid">The grid points.</param>
    private static void WriteLutHeader(byte[] table, string signature, int inputs, int grid)
    {
        Encoding.ASCII.GetBytes(signature).CopyTo(table, 0);
        table[ChannelsOffset] = (byte)inputs;
        table[ChannelsOffset + 1] = OutputChannels;
        table[GridPointsOffset] = (byte)grid;

        // The matrix is the identity: its diagonal, every fourth number, is one.
        table.AsSpan(LutMatrixOffset, LutMatrixSize).Clear();
        for (var i = 0; i < OutputChannels; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(table.AsSpan(LutMatrixOffset + (i * DiagonalStep * Number32Size)), (int)FixedScale);
        }
    }

    /// <summary>Writes a rounded 16-bit value.</summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="position">The position.</param>
    /// <param name="value">The value, from 0 to 65535.</param>
    /// <returns>The position after the value.</returns>
    private static int WriteWide(byte[] buffer, int position, double value)
    {
        BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(position), (ushort)Math.Round(Math.Clamp(value, 0, Maximum16)));
        return position + WideBytes;
    }

    /// <summary>Joins tags that are stored back to back, each padded to four bytes.</summary>
    /// <param name="curves">The curve tags.</param>
    /// <returns>The joined bytes.</returns>
    private static byte[] Concatenate(byte[][] curves)
    {
        var all = new List<byte>();
        foreach (var curve in curves)
        {
            all.AddRange(curve);
            while (all.Count % TagAlignment != 0)
            {
                all.Add(0);
            }
        }

        return [.. all];
    }

    /// <summary>Writes the matrix and offsets of an 'mAB ' tag.</summary>
    /// <param name="matrix">The twelve values: nine coefficients then three offsets.</param>
    /// <returns>The bytes.</returns>
    private static byte[] MatrixBytes(double[] matrix)
    {
        var bytes = new byte[matrix.Length * Number32Size];
        for (var i = 0; i < matrix.Length; i++)
        {
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(i * Number32Size), (int)Math.Round(matrix[i] * FixedScale));
        }

        return bytes;
    }

    /// <summary>Writes the table of an 'mAB ' tag.</summary>
    /// <param name="grids">The grid points of each channel.</param>
    /// <param name="map">Maps positions to stored values.</param>
    /// <param name="precision">The bytes per sample, 1 or 2.</param>
    /// <returns>The bytes.</returns>
    private static byte[] ClutBytes(int[] grids, Func<double[], double[]> map, int precision)
    {
        var values = Samples(grids, map).ToArray();
        var bytes = new byte[MabClutHeader + (values.Length * precision)];
        for (var i = 0; i < grids.Length; i++)
        {
            bytes[i] = (byte)grids[i];
        }

        bytes[MabGridBytes] = (byte)precision;
        for (var i = 0; i < values.Length; i++)
        {
            if (precision == 1)
            {
                bytes[MabClutHeader + i] = (byte)Math.Round(values[i] * Maximum8);
            }
            else
            {
                _ = WriteWide(bytes, MabClutHeader + (i * WideBytes), values[i] * Maximum16);
            }
        }

        return bytes;
    }

    /// <summary>Raises an integer to a power.</summary>
    /// <param name="value">The base.</param>
    /// <param name="exponent">The exponent.</param>
    /// <returns>The result.</returns>
    private static int Pow(int value, int exponent)
    {
        var result = 1;
        for (var i = 0; i < exponent; i++)
        {
            result *= value;
        }

        return result;
    }

    /// <summary>Rounds a length up to the tag alignment.</summary>
    /// <param name="length">The length.</param>
    /// <returns>The aligned length.</returns>
    private static int Aligned(int length) => (length + TagAlignment - 1) / TagAlignment * TagAlignment;

    /// <summary>A profile tag.</summary>
    /// <param name="Signature">The four-character signature.</param>
    /// <param name="Data">The tag data.</param>
    internal readonly record struct Tag(string Signature, byte[] Data);

    /// <summary>The stages of an 'mAB ' tag beyond its table.</summary>
    /// <param name="OutputCurves">The three output (B) curve tags.</param>
    /// <param name="Matrix">The twelve matrix values, or <see langword="null"/> for none.</param>
    /// <param name="MatrixCurves">The three matrix (M) curve tags, or <see langword="null"/> for none.</param>
    /// <param name="InputCurves">The input (A) curve tags, or <see langword="null"/> for none.</param>
    /// <param name="Precision">The bytes per table sample, 1 or 2.</param>
    internal readonly record struct MabStages(byte[][] OutputCurves, double[]? Matrix, byte[][]? MatrixCurves, byte[][]? InputCurves, int Precision);
}
