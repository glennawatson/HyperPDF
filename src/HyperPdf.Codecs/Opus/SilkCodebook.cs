// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Opus;

/// <summary>The line spectral frequency codebook for a bandwidth: one for 8 and 12 kHz, and one for 16 kHz.</summary>
/// <param name="Wide"><see langword="true"/> for the 16 kHz codebook.</param>
internal readonly record struct SilkCodebook(bool Wide)
{
    /// <summary>The number of vectors in the first stage.</summary>
    internal const int Count = 32;

    /// <summary>The step of the quantiser of the narrowband and mediumband residual, 0.18 in Q16.</summary>
    private const int NarrowStep = 11_796;

    /// <summary>The step of the quantiser of the wideband residual, 0.15 in Q16.</summary>
    private const int WideStep = 9_830;

    /// <summary>The order of the narrowband and mediumband filter.</summary>
    private const int NarrowOrder = 10;

    /// <summary>The order of the wideband filter.</summary>
    private const int WideOrder = 16;

    /// <summary>Gets the order of the filter the codebook describes.</summary>
    internal int Order => Wide ? WideOrder : NarrowOrder;

    /// <summary>Gets the step of the quantiser of the residual, in Q16.</summary>
    internal int QuantStep => Wide ? WideStep : NarrowStep;

    /// <summary>Gets the vectors of the first stage, in Q8.</summary>
    internal ReadOnlySpan<byte> Vectors => Wide ? SilkCodebookTables.WideVectors : SilkCodebookTables.NarrowVectors;

    /// <summary>Gets the inverse cumulative frequencies of the first stage index, for unvoiced then voiced frames.</summary>
    internal ReadOnlySpan<byte> IndexIcdf => Wide ? SilkCodebookTables.WideIndexIcdf : SilkCodebookTables.NarrowIndexIcdf;

    /// <summary>Gets how the second stage is coded for each vector.</summary>
    internal ReadOnlySpan<byte> Select => Wide ? SilkCodebookTables.WideSelect : SilkCodebookTables.NarrowSelect;

    /// <summary>Gets the inverse cumulative frequencies of the residuals of the second stage.</summary>
    internal ReadOnlySpan<byte> ResidualIcdf => Wide ? SilkCodebookTables.WideResidualIcdf : SilkCodebookTables.NarrowResidualIcdf;

    /// <summary>Gets the predictors of the residuals, in Q8.</summary>
    internal ReadOnlySpan<byte> Predictor => Wide ? SilkCodebookTables.WidePredictor : SilkCodebookTables.NarrowPredictor;

    /// <summary>Gets the least distance between neighbouring frequencies, in Q15.</summary>
    internal ReadOnlySpan<short> MinimumDistance => Wide ? SilkCodebookTables.WideMinimumDistance : SilkCodebookTables.NarrowMinimumDistance;
}
