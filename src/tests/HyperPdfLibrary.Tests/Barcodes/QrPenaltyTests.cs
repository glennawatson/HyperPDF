// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Barcodes;

namespace HyperPdfLibrary.Tests.Barcodes;

/// <summary>Checks that the packed QR penalty gives exactly the score of the module by module one.</summary>
public sealed class QrPenaltyTests
{
    /// <summary>The first state of the generator, so the symbols are the same on every run.</summary>
    private const uint Seed = 20_260_101;

    /// <summary>The multiplier of the linear congruential generator.</summary>
    private const uint Multiplier = 1_664_525;

    /// <summary>The increment of the linear congruential generator.</summary>
    private const uint Increment = 1_013_904_223;

    /// <summary>The bits of the generator's state that are random enough to use.</summary>
    private const int UsefulShift = 16;

    /// <summary>How many random symbols to compare at each size.</summary>
    private const int SymbolsPerSize = 40;

    /// <summary>The smallest QR Code size, in modules across.</summary>
    private const int SmallestSize = 21;

    /// <summary>The distance between QR Code sizes.</summary>
    private const int SizeStep = 4;

    /// <summary>The number of QR Code versions to try.</summary>
    private const int Versions = 27;

    /// <summary>The chances out of ten that a module is dark in the sparse symbols.</summary>
    private const uint SparseDark = 2;

    /// <summary>The chances out of ten that a module is dark in the dense symbols.</summary>
    private const uint DenseDark = 8;

    /// <summary>The chances out of ten that a module is dark in the balanced symbols.</summary>
    private const uint BalancedDark = 5;

    /// <summary>The chances in all, for the dark share.</summary>
    private const uint Tenths = 10;

    /// <summary>The modules across the striped symbols.</summary>
    private const int StripedSize = 29;

    /// <summary>The divisor that makes a stripe dark for half its period.</summary>
    private const int Half = 2;

    /// <summary>The periods of the stripes, which put long runs and finder look-alikes in both directions.</summary>
    private static readonly int[] Periods = [3, 7, 11, 13];

    /// <summary>Random symbols of every size from version 1 to 27 score the same both ways.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PackedScoreEqualsTheScalarScore()
    {
        var state = Seed;
        foreach (var share in new[] { SparseDark, BalancedDark, DenseDark })
        {
            for (var version = 1; version <= Versions; version++)
            {
                var size = SmallestSize + ((version - 1) * SizeStep);
                for (var i = 0; i < SymbolsPerSize; i++)
                {
                    var modules = new bool[size * size];
                    for (var m = 0; m < modules.Length; m++)
                    {
                        state = (state * Multiplier) + Increment;
                        modules[m] = (state >> UsefulShift) % Tenths < share;
                    }

                    await Assert.That(QrPackedPenalty.Score(modules, size, int.MaxValue)).IsEqualTo(QrPenalty.ScoreScalar(modules, size, int.MaxValue));
                }
            }
        }
    }

    /// <summary>Striped symbols with long runs and finder look-alikes in both directions score the same both ways.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StripedSymbolsScoreTheSame()
    {
        foreach (var period in Periods)
        {
            var modules = new bool[StripedSize * StripedSize];
            for (var y = 0; y < StripedSize; y++)
            {
                for (var x = 0; x < StripedSize; x++)
                {
                    modules[(y * StripedSize) + x] = ((x + (y / period)) % period) < (period / Half);
                }
            }

            await Assert.That(QrPackedPenalty.Score(modules, StripedSize, int.MaxValue)).IsEqualTo(QrPenalty.ScoreScalar(modules, StripedSize, int.MaxValue));
        }
    }
}
