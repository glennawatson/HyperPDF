// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Tests.Documents;

/// <summary>Tests for <see cref="IntegrityWarnings"/>, which decides when an integrity check is worth a notice.</summary>
public sealed class IntegrityWarningsTests
{
    /// <summary>A document with no integrity protection, or with protection that was checked and holds, says nothing.</summary>
    /// <param name="status">The status.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(DocumentIntegrityStatus.NotProtected)]
    [Arguments(DocumentIntegrityStatus.Intact)]
    public async Task SaysNothingWhenNothingIsWrong(DocumentIntegrityStatus status) =>
        await Assert.That(IntegrityWarnings.Describe(new(status, "text"))).IsNull();

    /// <summary>A document that has no result says nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SaysNothingWithoutAResult() => await Assert.That(IntegrityWarnings.Describe(null)).IsNull();

    /// <summary>Changed, missing and unchecked protection are all told to the reader, with the sentence of the check.</summary>
    /// <param name="status">The status.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(DocumentIntegrityStatus.Invalid)]
    [Arguments(DocumentIntegrityStatus.Missing)]
    [Arguments(DocumentIntegrityStatus.Unverified)]
    public async Task WarnsWhenTheDocumentIsNotKnownToBeIntact(DocumentIntegrityStatus status) =>
        await Assert.That(IntegrityWarnings.Describe(new(status, "The check failed."))).IsEqualTo("The check failed.");
}
