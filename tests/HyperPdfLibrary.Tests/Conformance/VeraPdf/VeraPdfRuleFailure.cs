// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>A rule veraPDF reports as failed.</summary>
/// <param name="Specification">The standard, such as "ISO 19005-1:2005".</param>
/// <param name="Clause">The clause, such as 6.1.4.</param>
/// <param name="TestNumber">The test within the clause.</param>
internal sealed record VeraPdfRuleFailure(string Specification, string Clause, string TestNumber)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Specification} §{Clause} test {TestNumber}";
}
