// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Checks resource classification from parsed values and explicit operation cancellation.</summary>
public sealed class PdfAConversionResourcesTests
{
    /// <summary>Allowed image depths retain their part-specific sixteen-bit restriction.</summary>
    /// <param name="depth">The image component depth.</param>
    /// <param name="wideAllowed">Whether sixteen bits are permitted.</param>
    /// <param name="expected">The expected classification.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(1, false, true)]
    [Arguments(2, false, true)]
    [Arguments(4, false, true)]
    [Arguments(8, false, true)]
    [Arguments(16, false, false)]
    [Arguments(16, true, true)]
    [Arguments(0, true, false)]
    [Arguments(-1, true, false)]
    [Arguments(int.MaxValue, true, false)]
    public async Task IsAllowedBitDepth_ParsedDepth_PreservesRestriction(int depth, bool wideAllowed, bool expected) =>
        await Assert.That(PdfAConversionResources.IsAllowedBitDepth(depth, wideAllowed)).IsEqualTo(expected);

    /// <summary>Both standard modes and legacy Compatible are recognised without decoding name strings.</summary>
    /// <param name="name">The parsed mode name.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(KnownName.Normal)]
    [Arguments(KnownName.Multiply)]
    [Arguments(KnownName.Screen)]
    [Arguments(KnownName.Overlay)]
    [Arguments(KnownName.Darken)]
    [Arguments(KnownName.Lighten)]
    [Arguments(KnownName.ColorDodge)]
    [Arguments(KnownName.ColorBurn)]
    [Arguments(KnownName.HardLight)]
    [Arguments(KnownName.SoftLight)]
    [Arguments(KnownName.Difference)]
    [Arguments(KnownName.Exclusion)]
    [Arguments(KnownName.Hue)]
    [Arguments(KnownName.Saturation)]
    [Arguments(KnownName.Color)]
    [Arguments(KnownName.Luminosity)]
    [Arguments(KnownName.Compatible)]
    public async Task IsKnownBlend_StandardMode_PreservesClassification(KnownName name)
    {
        var value = PdfValue.FromName(name);

        await Assert.That(PdfAConversionResources.IsKnownBlend(value, CancellationToken.None)).IsTrue();
    }

    /// <summary>An array is accepted only when every element is a recognised mode.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IsKnownBlend_Array_RejectsUnknownAndNonNameValues()
    {
        var array = new PdfArray(null, [PdfValue.FromName(KnownName.Normal), PdfValue.FromName(KnownName.Compatible)]);
        var value = PdfValue.FromArray(array);
        var known = PdfAConversionResources.IsKnownBlend(value, CancellationToken.None);
        array.Add(PdfValue.FromName(KnownName.Font));
        var unknown = PdfAConversionResources.IsKnownBlend(value, CancellationToken.None);
        var nonName = PdfAConversionResources.IsKnownBlend(PdfValue.FromInteger(0), CancellationToken.None);

        await Assert.That(known).IsTrue();
        await Assert.That(unknown).IsFalse();
        await Assert.That(nonName).IsFalse();
    }

    /// <summary>An explicitly cancelled operation stops before scanning the parsed value.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IsKnownBlend_Cancelled_ThrowsOperationCancelled()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var value = PdfValue.FromName(KnownName.Normal);

        await Assert.That(() => PdfAConversionResources.IsKnownBlend(value, cancelled.Token)).Throws<OperationCanceledException>();
    }
}
