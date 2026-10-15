// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The state of one PDF/A validation: the document, the level, and the failures the rule areas record. Areas record a
/// failure for any rule they detect; the report keeps only the rules the level applies.
/// </summary>
[DebuggerDisplay("PdfAValidationContext: {Level}, {_failures?.Count} rules failed")]
internal sealed class PdfAValidationContext
{
    /// <summary>The failures by rule, made when the first rule fails.</summary>
    private Dictionary<PdfARule, PdfAFailure>? _failures;

    /// <summary>Initializes a new instance of the <see cref="PdfAValidationContext"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level checked.</param>
    /// <param name="cancellationToken">Cancels the validation.</param>
    internal PdfAValidationContext(PdfDocument document, PdfALevel level, CancellationToken cancellationToken)
        : this(document, level, 0, cancellationToken)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfAValidationContext"/> class for a nested document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="level">The level checked.</param>
    /// <param name="depth">How many embedded files deep the document is; 0 for the document the caller passed.</param>
    /// <param name="cancellationToken">Cancels the validation.</param>
    internal PdfAValidationContext(PdfDocument document, PdfALevel level, int depth, CancellationToken cancellationToken)
    {
        Document = document;
        Level = level;
        Part = PdfALevels.Part(level);
        Depth = depth;
        CancellationToken = cancellationToken;
    }

    /// <summary>Gets the document.</summary>
    internal PdfDocument Document { get; }

    /// <summary>Gets the level checked.</summary>
    internal PdfALevel Level { get; }

    /// <summary>Gets the part of ISO 19005 checked: 1, 2, 3 or 4.</summary>
    internal int Part { get; }

    /// <summary>Gets how many embedded files deep the document is; 0 for the document the caller passed.</summary>
    internal int Depth { get; }

    /// <summary>Gets the token that cancels the validation.</summary>
    internal CancellationToken CancellationToken { get; }

    /// <summary>Records one failure of a rule.</summary>
    /// <param name="rule">The rule broken.</param>
    /// <param name="context">Where, such as "object 12"; <see langword="null"/> when the place has no name.</param>
    internal void Fail(PdfARule rule, string? context)
    {
        var failures = _failures ??= [];
        failures[rule] = failures.TryGetValue(rule, out var failure) ? failure with { Count = failure.Count + 1 } : new(1, context);
    }

    /// <summary>Records a failure when a condition does not hold.</summary>
    /// <param name="holds">Whether the rule holds.</param>
    /// <param name="rule">The rule.</param>
    /// <param name="context">Where, or <see langword="null"/>.</param>
    internal void Require(bool holds, PdfARule rule, string? context)
    {
        if (!holds)
        {
            Fail(rule, context);
        }
    }

    /// <summary>Gets the failures recorded for a rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns>The failure, or <see langword="null"/> when the rule held.</returns>
    internal PdfAFailure? FailureOf(PdfARule rule) => _failures is not null && _failures.TryGetValue(rule, out var failure) ? failure : null;
}
