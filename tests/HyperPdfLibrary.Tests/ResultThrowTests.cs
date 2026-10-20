// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Tests;

/// <summary>Checks the throwing helpers on the structured result types.</summary>
public sealed class ResultThrowTests
{
    /// <summary>A written protection result is returned as it is.</summary>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    public async Task WrittenProtectionIsReturned()
    {
        var result = new PdfProtectionResult(null, ["note"]);

        await Assert.That(result.ThrowIfRefused()).IsSameReferenceAs(result);
    }

    /// <summary>A refused protection result throws a security error holding the refusal.</summary>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    public async Task RefusedProtectionThrowsWithTheRefusal()
    {
        var result = new PdfProtectionResult("No owner password was given.", []);

        var exception = Assert.Throws<PdfException>(() => _ = result.ThrowIfRefused());

        await Assert.That(exception.Error).IsEqualTo(PdfError.Security);
        await Assert.That(exception.Message).IsEqualTo("No owner password was given.");
    }

    /// <summary>An applied field change is returned as it is.</summary>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    public async Task AppliedFieldChangeIsReturned()
    {
        var result = PdfFieldChangeResult.Done;

        await Assert.That(result.ThrowIfRefused()).IsSameReferenceAs(result);
    }

    /// <summary>A refused field change throws and keeps the reason.</summary>
    /// <returns>A task that completes when the checks finish.</returns>
    [Test]
    public async Task RefusedFieldChangeThrowsWithTheReason()
    {
        var result = PdfFieldChangeResult.Refused("The field is read-only.");

        var exception = Assert.Throws<InvalidOperationException>(() => _ = result.ThrowIfRefused());

        await Assert.That(exception.Message).IsEqualTo("The field is read-only.");
    }
}
