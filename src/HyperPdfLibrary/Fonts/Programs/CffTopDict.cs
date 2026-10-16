// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The entries of a CFF Top DICT, or of a Font DICT in a CID font's FDArray.</summary>
/// <param name="CharStrings">The offset of the CharStrings INDEX, or -1.</param>
/// <param name="Charset">The charset offset, or a predefined charset id (0 to 2).</param>
/// <param name="Encoding">The encoding offset, or a predefined encoding id (0 or 1).</param>
/// <param name="PrivateSize">The size of the Private DICT.</param>
/// <param name="PrivateOffset">The offset of the Private DICT, or -1.</param>
/// <param name="FdArray">The offset of the FDArray INDEX, or -1.</param>
/// <param name="FdSelect">The offset of the FDSelect table, or -1.</param>
/// <param name="IsCid">Whether the DICT has a ROS entry, making the font CID-keyed.</param>
/// <param name="Matrix">The font matrix, or <see langword="null"/> when the DICT gives none.</param>
/// <param name="BoundingBox">The font bounding box.</param>
[DebuggerDisplay("CffTopDict: cid {IsCid}")]
internal readonly record struct CffTopDict(
    int CharStrings,
    int Charset,
    int Encoding,
    int PrivateSize,
    int PrivateOffset,
    int FdArray,
    int FdSelect,
    bool IsCid,
    FontMatrix? Matrix,
    PdfRectangle BoundingBox)
{
    /// <summary>The FontBBox operator.</summary>
    private const int FontBBoxOperator = 5;

    /// <summary>The charset operator.</summary>
    private const int CharsetOperator = 15;

    /// <summary>The Encoding operator.</summary>
    private const int EncodingOperator = 16;

    /// <summary>The CharStrings operator.</summary>
    private const int CharStringsOperator = 17;

    /// <summary>The Private operator.</summary>
    private const int PrivateOperator = 18;

    /// <summary>The FontMatrix operator.</summary>
    private const int FontMatrixOperator = (CffDictReader.Escape << CffDictReader.EscapeShift) | 7;

    /// <summary>The ROS operator.</summary>
    private const int RosOperator = (CffDictReader.Escape << CffDictReader.EscapeShift) | 30;

    /// <summary>The FDArray operator.</summary>
    private const int FdArrayOperator = (CffDictReader.Escape << CffDictReader.EscapeShift) | 36;

    /// <summary>The FDSelect operator.</summary>
    private const int FdSelectOperator = (CffDictReader.Escape << CffDictReader.EscapeShift) | 37;

    /// <summary>The index of the third operand.</summary>
    private const int Third = 2;

    /// <summary>The index of the fourth operand.</summary>
    private const int Fourth = 3;

    /// <summary>The index of the fifth operand.</summary>
    private const int Fifth = 4;

    /// <summary>The index of the sixth operand.</summary>
    private const int Sixth = 5;

    /// <summary>The operands of the font matrix.</summary>
    private const int MatrixOperands = 6;

    /// <summary>Gets a DICT with every entry at its default.</summary>
    internal static CffTopDict Default => new(-1, 0, 0, 0, -1, -1, -1, false, null, default);

    /// <summary>Reads a DICT.</summary>
    /// <param name="dict">The DICT data.</param>
    /// <returns>The entries.</returns>
    internal static CffTopDict Read(ReadOnlySpan<byte> dict)
    {
        var top = Default;
        var reader = new CffDictReader(dict, stackalloc double[CharstringLimits.StackDepth]);
        while (reader.TryRead(out var op))
        {
            top = top.ApplyOffset(op, ref reader).ApplyGeometry(op, ref reader);
        }

        return top;
    }

    /// <summary>Reads a declared length or offset without accepting a negative value.</summary>
    /// <param name="reader">The operand reader.</param>
    /// <param name="index">The operand index.</param>
    /// <returns>The nonnegative integer.</returns>
    /// <exception cref="PdfException">The declared length or offset is negative.</exception>
    private static int ReadOffset(ref CffDictReader reader, int index)
    {
        var value = reader.GetInt(index);
        if (value < 0)
        {
            throw new PdfException(PdfError.Format, "The CFF dictionary length or offset is negative.");
        }

        return value;
    }

    /// <summary>Applies an entry that holds an offset.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader holding the operands.</param>
    /// <returns>The updated DICT.</returns>
    private CffTopDict ApplyOffset(int op, ref CffDictReader reader) => op switch
    {
        CharsetOperator => this with { Charset = ReadOffset(ref reader, 0) },
        EncodingOperator => this with { Encoding = ReadOffset(ref reader, 0) },
        CharStringsOperator => this with { CharStrings = ReadOffset(ref reader, 0) },
        PrivateOperator => this with { PrivateSize = ReadOffset(ref reader, 0), PrivateOffset = ReadOffset(ref reader, 1) },
        FdArrayOperator => this with { FdArray = ReadOffset(ref reader, 0) },
        FdSelectOperator => this with { FdSelect = ReadOffset(ref reader, 0) },
        _ => this,
    };

    /// <summary>Applies an entry that describes the font's shape or kind.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader holding the operands.</param>
    /// <returns>The updated DICT.</returns>
    private CffTopDict ApplyGeometry(int op, ref CffDictReader reader) => op switch
    {
        RosOperator => this with { IsCid = true },
        FontBBoxOperator => this with
        {
            BoundingBox = PdfRectangle.FromCorners((float)reader.Get(0), (float)reader.Get(1), (float)reader.Get(Third), (float)reader.Get(Fourth)),
        },
        FontMatrixOperator when reader.Count >= MatrixOperands => this with
        {
            Matrix = new FontMatrix(
                (float)reader.Get(0),
                (float)reader.Get(1),
                (float)reader.Get(Third),
                (float)reader.Get(Fourth),
                (float)reader.Get(Fifth),
                (float)reader.Get(Sixth)),
        },
        _ => this,
    };
}
