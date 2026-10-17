// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Text;
using HyperPdfLibrary.Fonts.Generation;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Checks the canonical single-scalar table against runtime normalization.</summary>
public sealed class UnicodeCanonicalScalarsTests
{
    /// <summary>The largest Unicode scalar.</summary>
    private const int MaxScalar = 0x10FFFF;

    /// <summary>The runtime rejects this noncharacter during normalization.</summary>
    private const int InvalidNormalizationScalar = 0xFFFE;

    /// <summary>The number of scalar values between cancellation checks.</summary>
    private const int CancellationChunk = 256;

    /// <summary>Every normalizable scalar keeps the same single-scalar fallback as NFC.</summary>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Resolve_AllScalars_AgreesWithCanonicalNormalization(CancellationToken cancellationToken)
    {
        var mismatch = -1;
        for (var value = 0; value <= MaxScalar; value++)
        {
            if (value % CancellationChunk == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!Rune.IsValid(value) || value == InvalidNormalizationScalar)
            {
                continue;
            }

            var normalized = char.ConvertFromUtf32(value).Normalize(NormalizationForm.FormC);
            var expected = Rune.DecodeFromUtf16(normalized, out var rune, out var consumed) == OperationStatus.Done
                && consumed == normalized.Length ? rune.Value : value;
            if (UnicodeCanonicalScalars.Resolve(value) == expected)
            {
                continue;
            }

            mismatch = value;
            break;
        }

        await Assert.That(mismatch).IsEqualTo(-1);
    }

    /// <summary>Scalar lookup creates no decoded strings or runtime normalization storage.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Resolve_CanonicalScalar_DoesNotAllocate()
    {
        const int Angstrom = 0x212B;
        const int Expected = 0xC5;
        _ = UnicodeCanonicalScalars.Resolve(Angstrom);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var actual = UnicodeCanonicalScalars.Resolve(Angstrom);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await Assert.That(actual).IsEqualTo(Expected);
        await Assert.That(allocated).IsEqualTo(0);
    }
}
