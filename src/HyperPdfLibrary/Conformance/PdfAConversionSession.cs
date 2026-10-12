// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The state of one PDF/A conversion: the private working copy, the level, and the changes and problems the steps record.
/// Each note names the clause of the rule it answers, as the target level numbers it.
/// </summary>
[DebuggerDisplay("PdfAConversionSession: {Level}, {_changes.Count} changes, {_problems.Count} problems")]
internal sealed class PdfAConversionSession
{
    /// <summary>The changes made.</summary>
    private readonly List<PdfAConversionNote> _changes = [];

    /// <summary>The reasons the conversion cannot finish.</summary>
    private readonly List<PdfAConversionNote> _problems = [];

    /// <summary>Initializes a new instance of the <see cref="PdfAConversionSession"/> class.</summary>
    /// <param name="document">The working copy, which the session may change freely.</param>
    /// <param name="level">The level asked for.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    internal PdfAConversionSession(PdfDocument document, PdfALevel level, CancellationToken cancellationToken)
    {
        Document = document;
        Level = level;
        Part = PdfALevels.Part(level);
        CancellationToken = cancellationToken;
    }

    /// <summary>Gets the working copy.</summary>
    internal PdfDocument Document { get; }

    /// <summary>Gets the working copy's objects.</summary>
    internal PdfObjectStore Store => Document.Objects;

    /// <summary>Gets the level asked for.</summary>
    internal PdfALevel Level { get; }

    /// <summary>Gets the part of ISO 19005: 1, 2, 3 or 4.</summary>
    internal int Part { get; }

    /// <summary>Gets the token that cancels the conversion.</summary>
    internal CancellationToken CancellationToken { get; }

    /// <summary>Gets a value indicating whether a problem has stopped the conversion.</summary>
    internal bool HasProblems => _problems.Count > 0;

    /// <summary>Gets a value indicating whether the level needs Unicode for every character: levels A and U.</summary>
    internal bool NeedsUnicode => PdfALevels.Conformance(Level) is "A" or "U";

    /// <summary>Records a change.</summary>
    /// <param name="description">What changed, in plain words.</param>
    /// <param name="rules">The rules the change answers; the first one the level applies gives the clause.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Changed(string description, params ReadOnlySpan<PdfARule> rules) => _changes.Add(Note(description, rules));

    /// <summary>Records a reason the conversion cannot finish.</summary>
    /// <param name="description">The reason, in plain words.</param>
    /// <param name="rules">The rules that cannot be met; the first one the level applies gives the clause.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Refuse(string description, params ReadOnlySpan<PdfARule> rules) => _problems.Add(Note(description, rules));

    /// <summary>Records a problem found by checking the converted file.</summary>
    /// <param name="result">The failed rule.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Refuse(PdfARuleResult result) => _problems.Add(new(result.Specification, result.Clause, result.Description));

    /// <summary>Gets the changes made.</summary>
    /// <returns>The changes.</returns>
    internal PdfAConversionNote[] GetChanges() => [.. _changes];

    /// <summary>Gets the problems found.</summary>
    /// <returns>The problems.</returns>
    internal PdfAConversionNote[] GetProblems() => [.. _problems];

    /// <summary>Determines whether the level applies a rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal bool Applies(PdfARule rule)
    {
        foreach (var reference in PdfARuleCatalog.For(Level))
        {
            if (reference.Rule == rule)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Builds a note with the clause of the first rule the level applies.</summary>
    /// <param name="description">The text.</param>
    /// <param name="rules">The candidate rules.</param>
    /// <returns>The note.</returns>
    private PdfAConversionNote Note(string description, ReadOnlySpan<PdfARule> rules)
    {
        var specification = PdfALevels.Specification(Level);
        foreach (var rule in rules)
        {
            foreach (var reference in PdfARuleCatalog.For(Level))
            {
                if (reference.Rule == rule)
                {
                    return new(specification, reference.Clause, description);
                }
            }
        }

        return new(specification, string.Empty, description);
    }
}
