// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Tests that the report reads the PDF/UA amendment and corrigendum identifiers (ISO 14289-1, clause 5) without requiring them.</summary>
public sealed class AccessibilityClaimIdentifierTests
{
    /// <summary>The amendment identifier the test files carry.</summary>
    private const string Amendment = "1";

    /// <summary>The corrigendum identifier the test files carry.</summary>
    private const string Corrigendum = "2";

    /// <summary>The identifiers are reported next to the part and revision.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AmendmentAndCorrigendumAreReported()
    {
        var report = await AccessibilityPdfs.ReportAsync(new() { Xmp = AccessibilityPdfs.Packet(1, true, null, Amendment, Corrigendum) });

        await Assert.That(report.Claim.Part).IsEqualTo(1);
        await Assert.That(report.Claim.Amendment).IsEqualTo(Amendment);
        await Assert.That(report.Claim.Corrigendum).IsEqualTo(Corrigendum);
    }

    /// <summary>A file without the identifiers has none, and has the same findings as one with them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingIdentifiersAreNotAViolation()
    {
        var without = await AccessibilityPdfs.ReportAsync(new() { Xmp = AccessibilityPdfs.Packet(1, true) });
        var with = await AccessibilityPdfs.ReportAsync(new() { Xmp = AccessibilityPdfs.Packet(1, true, null, Amendment, Corrigendum) });

        await Assert.That(without.Claim.Amendment).IsNull();
        await Assert.That(without.Claim.Corrigendum).IsNull();
        await Assert.That(without.TotalCount).IsEqualTo(with.TotalCount);
    }
}
