// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The enumerated parameters of the CIELab colour space (ISO 15444-2 M.11.7.4.1): the range and offset that turn the
/// unsigned code values into L*, a* and b*, and the illuminant.
/// </summary>
/// <param name="RangeL">RL, the range of L*.</param>
/// <param name="OffsetL">OL, the offset of L*.</param>
/// <param name="RangeA">RA, the range of a*.</param>
/// <param name="OffsetA">OA, the offset of a*.</param>
/// <param name="RangeB">RB, the range of b*.</param>
/// <param name="OffsetB">OB, the offset of b*.</param>
/// <param name="Illuminant">IL, the illuminant code, as in annex E of the T.4 fax recommendation.</param>
internal readonly record struct JpxLabParameters(uint RangeL, uint OffsetL, uint RangeA, uint OffsetA, uint RangeB, uint OffsetB, uint Illuminant)
{
    /// <summary>The bytes of the EP field of a CIELab specification: six 32-bit values and the illuminant.</summary>
    internal const int Bytes = 28;

    /// <summary>The offset of the second value in the field.</summary>
    private const int OffsetLField = 4;

    /// <summary>The offset of the third value in the field.</summary>
    private const int RangeAField = 8;

    /// <summary>The offset of the fourth value in the field.</summary>
    private const int OffsetAField = 12;

    /// <summary>The offset of the fifth value in the field.</summary>
    private const int RangeBField = 16;

    /// <summary>The offset of the sixth value in the field.</summary>
    private const int OffsetBField = 20;

    /// <summary>The offset of the illuminant in the field.</summary>
    private const int IlluminantField = 24;

    /// <summary>Reads the EP field.</summary>
    /// <param name="field">The field, <see cref="Bytes"/> long.</param>
    /// <returns>The parameters.</returns>
    internal static JpxLabParameters Read(ReadOnlySpan<byte> field) => new(
        BinaryPrimitives.ReadUInt32BigEndian(field),
        BinaryPrimitives.ReadUInt32BigEndian(field[OffsetLField..]),
        BinaryPrimitives.ReadUInt32BigEndian(field[RangeAField..]),
        BinaryPrimitives.ReadUInt32BigEndian(field[OffsetAField..]),
        BinaryPrimitives.ReadUInt32BigEndian(field[RangeBField..]),
        BinaryPrimitives.ReadUInt32BigEndian(field[OffsetBField..]),
        BinaryPrimitives.ReadUInt32BigEndian(field[IlluminantField..]));
}
