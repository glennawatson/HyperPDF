// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Flac;

/// <summary>Reads the residual of a subframe: the differences between the samples and what the predictor guessed, coded in partitions with Rice codes.</summary>
internal static class FlacResidual
{
    /// <summary>The bits of the coding method.</summary>
    private const int MethodBits = 2;

    /// <summary>The bits of the partition order.</summary>
    private const int PartitionOrderBits = 4;

    /// <summary>The highest coding method defined.</summary>
    private const uint LastMethod = 1;

    /// <summary>The bits of the Rice parameter of the first method.</summary>
    private const int ParameterBits = 4;

    /// <summary>The bits of the Rice parameter of the second method.</summary>
    private const int LongParameterBits = 5;

    /// <summary>The bits that say how many bits each sample of an unencoded partition has.</summary>
    private const int EscapedWidthBits = 5;

    /// <summary>Reads the residual.</summary>
    /// <param name="reader">The bits, positioned at the start of the residual.</param>
    /// <param name="blockSize">The samples in the block, counting those the predictor takes as warm-up.</param>
    /// <param name="order">The samples at the start of the block that are warm-up and have no residual.</param>
    /// <param name="residual">Receives <c>blockSize - order</c> values.</param>
    /// <returns><see langword="false"/> when the residual is badly coded or runs past the end of the data.</returns>
    internal static bool Read(ref MsbBitReader reader, int blockSize, int order, Span<int> residual)
    {
        var method = reader.Read(MethodBits);
        var partitionOrder = (int)reader.Read(PartitionOrderBits);
        var partitions = 1 << partitionOrder;
        var partitionSize = blockSize >> partitionOrder;
        if (method > LastMethod || (blockSize & (partitions - 1)) != 0 || partitionSize < order)
        {
            return false;
        }

        var parameterBits = method == 0 ? ParameterBits : LongParameterBits;
        var escape = (1 << parameterBits) - 1;
        var written = 0;
        for (var partition = 0; partition < partitions; partition++)
        {
            var count = partition == 0 ? partitionSize - order : partitionSize;
            var parameter = (int)reader.Read(parameterBits);
            var target = residual.Slice(written, count);
            if (parameter == escape)
            {
                ReadEscaped(ref reader, target);
            }
            else
            {
                ReadRice(ref reader, parameter, target);
            }

            if (reader.IsOverrun)
            {
                return false;
            }

            written += count;
        }

        return true;
    }

    /// <summary>Reads a partition of Rice codes: a unary quotient, then the low bits of the remainder, folded so small numbers of both signs are short.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="parameter">The number of low bits.</param>
    /// <param name="target">Receives the values.</param>
    private static void ReadRice(ref MsbBitReader reader, int parameter, Span<int> target)
    {
        for (var index = 0; index < target.Length; index++)
        {
            var folded = ((uint)reader.ReadUnary() << parameter) | (parameter == 0 ? 0 : reader.Read(parameter));
            target[index] = (int)(folded >> 1) ^ -(int)(folded & 1);
        }
    }

    /// <summary>Reads a partition stored without coding, every value with the same number of bits.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="target">Receives the values.</param>
    private static void ReadEscaped(ref MsbBitReader reader, Span<int> target)
    {
        var width = (int)reader.Read(EscapedWidthBits);
        for (var index = 0; index < target.Length; index++)
        {
            target[index] = width == 0 ? 0 : reader.ReadSigned(width);
        }
    }
}
