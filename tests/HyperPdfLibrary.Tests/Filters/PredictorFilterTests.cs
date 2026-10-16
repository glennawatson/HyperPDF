// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Tests bounded row layouts and cancellation during predictor decoding.</summary>
public sealed class PredictorFilterTests
{
    /// <summary>The TIFF horizontal predictor.</summary>
    private const int Tiff = 2;

    /// <summary>The PNG predictor family.</summary>
    private const int Png = 12;

    /// <summary>The component depth of the ordinary byte layout.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes retained when a damaged row layout is ignored.</summary>
    private static readonly byte[] Original = [1, 1, 1];

    /// <summary>Damaged row dimensions pass through without arithmetic or slicing failures.</summary>
    /// <param name="predictor">The row predictor.</param>
    /// <param name="colors">The declared color components.</param>
    /// <param name="bits">The declared component width.</param>
    /// <param name="columns">The declared row width.</param>
    /// <param name="cancellationToken">Cancels this test.</param>
    /// <returns>A task representing the test.</returns>
    [Test]
    [Arguments(Png, 1, ByteBits, int.MaxValue)]
    [Arguments(Png, 1, ByteBits, -1)]
    [Arguments(Tiff, -1, ByteBits, -1)]
    [Arguments(Tiff, 1, int.MaxValue, 1)]
    [Arguments(Tiff, 1, -1, 1)]
    public async Task ApplyInvalidLayoutKeepsBytes(int predictor, int colors, int bits, int columns, CancellationToken cancellationToken)
    {
        var parameters = Parameters(predictor, colors, bits, columns);
        await Assert.That(Decode(parameters, cancellationToken)).IsEquivalentTo(Original);
    }

    /// <summary>A cancelled operation stops before changing the decoded rows.</summary>
    /// <param name="predictor">The row predictor.</param>
    /// <returns>A task representing the test.</returns>
    [Test]
    [Arguments(Tiff)]
    [Arguments(Png)]
    public async Task ApplyCancelledThrows(int predictor)
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var parameters = Parameters(predictor, 1, ByteBits, 1);
        await Assert.That(() => Decode(parameters, cancellation.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Constructs predictor dimensions without parsing test text.</summary>
    /// <param name="predictor">The predictor.</param>
    /// <param name="colors">The color components.</param>
    /// <param name="bits">The component width.</param>
    /// <param name="columns">The row width.</param>
    /// <returns>The decode parameters.</returns>
    private static PdfDictionary Parameters(int predictor, int colors, int bits, int columns)
    {
        var parameters = new PdfDictionary(null);
        parameters.Add(KnownName.Predictor, PdfValue.FromInteger(predictor));
        parameters.Add(KnownName.Colors, PdfValue.FromInteger(colors));
        parameters.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(bits));
        parameters.Add(KnownName.Columns, PdfValue.FromInteger(columns));
        return parameters;
    }

    /// <summary>Decodes a small row while returning rented output on every exit.</summary>
    /// <param name="parameters">The row layout.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] Decode(PdfDictionary parameters, CancellationToken cancellationToken)
    {
        var output = default(PooledBuffer);
        try
        {
            output.Write(Original);
            PredictorFilter.Apply(parameters, ref output, cancellationToken);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }
}
