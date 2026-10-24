// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Aac;

/// <summary>Steps over a program config element, which this decoder does not need because the channels come from the track.</summary>
internal static class AacProgramConfig
{
    /// <summary>The bits of the instance tag, the object type and the sample rate index.</summary>
    private const int LeadBits = 4 + 2 + 4;

    /// <summary>The bits of the count of front, side and back elements.</summary>
    private const int GroupCountBits = 4;

    /// <summary>The bits of the count of low frequency elements.</summary>
    private const int LowFrequencyCountBits = 2;

    /// <summary>The bits of the count of associated data elements.</summary>
    private const int DataCountBits = 3;

    /// <summary>The bits of the count of coupling elements.</summary>
    private const int CouplingCountBits = 4;

    /// <summary>The bits of a mono or stereo mixdown element number.</summary>
    private const int MixdownBits = 4;

    /// <summary>The bits of the matrix mixdown index and its pseudo surround flag.</summary>
    private const int MatrixBits = 3;

    /// <summary>The bits of a front, side, back or coupling element: a flag and a tag.</summary>
    private const int FlaggedElementBits = 5;

    /// <summary>The bits of a low frequency or data element: a tag.</summary>
    private const int TaggedElementBits = 4;

    /// <summary>The bits of the comment length.</summary>
    private const int CommentLengthBits = 8;

    /// <summary>The kinds of element with a count: front, side and back.</summary>
    private const int ElementGroups = 3;

    /// <summary>Skips the element, whose type bits are already read.</summary>
    /// <param name="reader">The bits.</param>
    internal static void Skip(ref MsbBitReader reader)
    {
        reader.Skip(LeadBits);
        var elements = 0;
        for (var group = 0; group < ElementGroups; group++)
        {
            elements += (int)reader.Read(GroupCountBits);
        }

        var lowFrequency = (int)reader.Read(LowFrequencyCountBits);
        var data = (int)reader.Read(DataCountBits);
        var coupling = (int)reader.Read(CouplingCountBits);
        SkipOptional(ref reader, MixdownBits);
        SkipOptional(ref reader, MixdownBits);
        SkipOptional(ref reader, MatrixBits);
        AacBits.Skip(ref reader, (elements * FlaggedElementBits) + (lowFrequency * TaggedElementBits) + (data * TaggedElementBits) + (coupling * FlaggedElementBits));
        reader.Align();
        AacBits.Skip(ref reader, (long)reader.Read(CommentLengthBits) * AacBits.ByteBits);
    }

    /// <summary>Skips a field that is there only when its flag is set.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="bits">The bits of the field.</param>
    private static void SkipOptional(ref MsbBitReader reader, int bits)
    {
        if (reader.ReadBit())
        {
            reader.Skip(bits);
        }
    }
}
