// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Finds a rule's result in a validation report by the clause and test its level numbers it by.</summary>
internal static class PdfARuleResults
{
    /// <summary>Determines whether a rule failed.</summary>
    /// <param name="report">The report.</param>
    /// <param name="rule">The rule.</param>
    /// <returns><see langword="true"/> when it failed.</returns>
    /// <exception cref="ArgumentException">The report's level does not apply the rule.</exception>
    internal static bool Failed(PdfAValidationReport report, PdfARule rule)
    {
        var reference = Reference(report.Level, rule);
        return Array.Exists(report.Failed, result => result.Clause == reference.Clause && result.Test == reference.Test);
    }

    /// <summary>Determines whether a rule was checked.</summary>
    /// <param name="report">The report.</param>
    /// <param name="rule">The rule.</param>
    /// <returns><see langword="true"/> when it was checked.</returns>
    /// <exception cref="ArgumentException">The report's level does not apply the rule.</exception>
    internal static bool Checked(PdfAValidationReport report, PdfARule rule)
    {
        var reference = Reference(report.Level, rule);
        return Array.Exists(report.Checked, result => result.Clause == reference.Clause && result.Test == reference.Test);
    }

    /// <summary>Gets the clause and test a level numbers a rule by.</summary>
    /// <param name="level">The level.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>The reference.</returns>
    /// <exception cref="ArgumentException">The level does not apply the rule.</exception>
    internal static PdfARuleReference Reference(PdfALevel level, PdfARule rule) =>
        Array.Find(PdfARuleCatalog.For(level), entry => entry.Rule == rule) is { Clause: not null } found
            ? found
            : throw new ArgumentException($"{PdfALevels.Label(level)} does not apply {rule}.", nameof(rule));
}
