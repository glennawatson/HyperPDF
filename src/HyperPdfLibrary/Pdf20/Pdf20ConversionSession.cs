// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>The state of one PDF 2.0 conversion: the private working copy, and the changes and problems the steps record.</summary>
[DebuggerDisplay("Pdf20ConversionSession: {_changes.Count} changes, {_problems.Count} problems")]
internal sealed class Pdf20ConversionSession
{
    /// <summary>The standard every note of the conversion cites unless it names another.</summary>
    internal const string Specification = "ISO 32000-2:2020";

    /// <summary>The changes made.</summary>
    private readonly List<Pdf20ConversionNote> _changes = [];

    /// <summary>The reasons the conversion cannot finish.</summary>
    private readonly List<Pdf20ConversionNote> _problems = [];

    /// <summary>Initializes a new instance of the <see cref="Pdf20ConversionSession"/> class.</summary>
    /// <param name="document">The working copy, which the session may change freely.</param>
    /// <param name="cancellationToken">Cancels the conversion.</param>
    internal Pdf20ConversionSession(PdfDocument document, CancellationToken cancellationToken)
    {
        Document = document;
        CancellationToken = cancellationToken;
        Names = new(document.Objects.Names);
    }

    /// <summary>Gets the working copy.</summary>
    internal PdfDocument Document { get; }

    /// <summary>Gets the working copy's objects.</summary>
    internal PdfObjectStore Store => Document.Objects;

    /// <summary>Gets the names the conversion looks for.</summary>
    internal Pdf20ConversionNames Names { get; }

    /// <summary>Gets the token that cancels the conversion.</summary>
    internal CancellationToken CancellationToken { get; }

    /// <summary>Gets a value indicating whether a problem has stopped the conversion.</summary>
    internal bool HasProblems => _problems.Count > 0;

    /// <summary>Records a change.</summary>
    /// <param name="clause">The clause of ISO 32000-2 behind it.</param>
    /// <param name="description">What changed, in plain words.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Changed(string clause, string description) => _changes.Add(new(Specification, clause, description));

    /// <summary>Records a reason the conversion cannot finish.</summary>
    /// <param name="clause">The clause of ISO 32000-2 behind it.</param>
    /// <param name="description">The reason, in plain words.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Refuse(string clause, string description) => _problems.Add(new(Specification, clause, description));

    /// <summary>Gets the changes made.</summary>
    /// <returns>The changes.</returns>
    internal Pdf20ConversionNote[] GetChanges() => [.. _changes];

    /// <summary>Gets the problems found.</summary>
    /// <returns>The problems.</returns>
    internal Pdf20ConversionNote[] GetProblems() => [.. _problems];
}
