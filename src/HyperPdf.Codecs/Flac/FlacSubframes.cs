// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Bits;

namespace HyperPdf.Codecs.Flac;

/// <summary>Reads the ways a FLAC subframe can be coded into a plane of samples.</summary>
internal static class FlacSubframes
{
    /// <summary>The bits of the precision of the coefficients.</summary>
    private const int PrecisionBits = 4;

    /// <summary>The precision field value that is not allowed.</summary>
    private const int InvalidPrecision = 16;

    /// <summary>The bits of the shift of a linear predictor.</summary>
    private const int ShiftBits = 5;

    /// <summary>Reads a block of one repeated value.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="plane">Receives the samples.</param>
    /// <param name="blockSize">The samples in the block.</param>
    /// <param name="bits">The bits of the value.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    internal static bool ReadConstant(ref MsbBitReader reader, int[] plane, int blockSize, int bits)
    {
        plane.AsSpan(FlacPredictors.Padding, blockSize).Fill(reader.ReadSigned(bits));
        return true;
    }

    /// <summary>Reads a block stored without prediction.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="plane">Receives the samples.</param>
    /// <param name="blockSize">The samples in the block.</param>
    /// <param name="bits">The bits of each sample.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    internal static bool ReadVerbatim(ref MsbBitReader reader, int[] plane, int blockSize, int bits)
    {
        var samples = plane.AsSpan(FlacPredictors.Padding, blockSize);
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = reader.ReadSigned(bits);
        }

        return true;
    }

    /// <summary>Reads a block coded with a fixed predictor.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="plane">Receives the samples.</param>
    /// <param name="blockSize">The samples in the block.</param>
    /// <param name="bits">The bits of each sample.</param>
    /// <param name="order">The order of the predictor.</param>
    /// <returns><see langword="false"/> when the block is shorter than the order or the residual is bad.</returns>
    internal static bool ReadFixed(ref MsbBitReader reader, int[] plane, int blockSize, int bits, int order)
    {
        if (!ReadWarmUp(ref reader, plane, blockSize, bits, order))
        {
            return false;
        }

        var samples = plane.AsSpan(FlacPredictors.Padding, blockSize);
        if (!FlacResidual.Read(ref reader, blockSize, order, samples[order..]))
        {
            return false;
        }

        FlacPredictors.RestoreFixed(plane, blockSize, order);
        return true;
    }

    /// <summary>Reads a block coded with a linear predictor.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="plane">Receives the samples.</param>
    /// <param name="blockSize">The samples in the block.</param>
    /// <param name="bits">The bits of each sample.</param>
    /// <param name="coefficients">Room for the weights; the first <paramref name="order"/> are used.</param>
    /// <param name="order">The number of weights.</param>
    /// <returns><see langword="false"/> when the block is shorter than the order or anything is bad.</returns>
    internal static bool ReadLinear(ref MsbBitReader reader, int[] plane, int blockSize, int bits, int[] coefficients, int order)
    {
        if (!ReadWarmUp(ref reader, plane, blockSize, bits, order))
        {
            return false;
        }

        var precision = (int)reader.Read(PrecisionBits) + 1;
        var shift = reader.ReadSigned(ShiftBits);
        if (precision == InvalidPrecision || shift < 0)
        {
            return false;
        }

        for (var tap = 0; tap < order; tap++)
        {
            coefficients[tap] = reader.ReadSigned(precision);
        }

        var samples = plane.AsSpan(FlacPredictors.Padding, blockSize);
        if (!FlacResidual.Read(ref reader, blockSize, order, samples[order..]))
        {
            return false;
        }

        FlacPredictors.RestoreLinear(plane, blockSize, coefficients.AsSpan(0, order), shift, bits);
        return true;
    }

    /// <summary>Reads the samples a predictor starts from, which are stored without prediction.</summary>
    /// <param name="reader">The bits.</param>
    /// <param name="plane">Receives the samples.</param>
    /// <param name="blockSize">The samples in the block.</param>
    /// <param name="bits">The bits of each sample.</param>
    /// <param name="order">The number of warm-up samples.</param>
    /// <returns><see langword="false"/> when the block is shorter than the warm-up.</returns>
    private static bool ReadWarmUp(ref MsbBitReader reader, int[] plane, int blockSize, int bits, int order)
    {
        if (order > blockSize)
        {
            return false;
        }

        for (var index = 0; index < order; index++)
        {
            plane[FlacPredictors.Padding + index] = reader.ReadSigned(bits);
        }

        return true;
    }
}
