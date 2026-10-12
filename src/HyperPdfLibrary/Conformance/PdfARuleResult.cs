// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>A PDF/A rule and what the validator found for it.</summary>
/// <param name="Specification">The standard, such as "ISO 19005-2:2011".</param>
/// <param name="Clause">The clause, such as 6.1.7.1.</param>
/// <param name="Test">The test number within the clause, as veraPDF numbers it.</param>
/// <param name="Description">What the rule requires, in the words of the veraPDF profiles.</param>
/// <param name="Failures">How many objects break the rule; 0 when the rule holds.</param>
/// <param name="Context">Where the first failure was found, such as "object 12" or "page 3"; <see langword="null"/> when the rule holds or the place has no name.</param>
[DebuggerDisplay("PdfARuleResult: {Clause} test {Test}, {Failures} failures")]
public sealed record PdfARuleResult(string Specification, string Clause, string Test, string Description, int Failures, string? Context)
{
    /// <summary>Gets a value indicating whether the rule holds.</summary>
    public bool Passed => Failures == 0;
}
