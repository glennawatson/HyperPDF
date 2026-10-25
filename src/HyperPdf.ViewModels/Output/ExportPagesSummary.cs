// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Output;

/// <summary>What an export run wrote.</summary>
/// <param name="Files">How many files were written.</param>
/// <param name="Skipped">How many things the format cannot hold that were left out.</param>
/// <param name="Approximated">How many things were drawn differently from the page.</param>
/// <param name="Path">The path the person chose.</param>
/// <param name="Problem">Why the run stopped, or <see langword="null"/>.</param>
[DebuggerDisplay("ExportPagesSummary: {Files} files")]
internal sealed record ExportPagesSummary(int Files, int Skipped, int Approximated, string Path, string? Problem)
{
    /// <summary>Gets the retained reason an export was refused.</summary>
    internal LocalizedMessage ProblemMessage { get; init; } = LocalizedMessage.Literal(Problem);

    /// <summary>Makes a refused summary while retaining the message resource.</summary>
    /// <param name="problem">The retained reason.</param>
    /// <returns>The summary.</returns>
    internal static ExportPagesSummary Refused(in LocalizedMessage problem) => new(0, 0, 0, string.Empty, problem.Value) { ProblemMessage = problem };

    /// <summary>Makes the summary of a run that stopped.</summary>
    /// <param name="problem">Why, in plain words.</param>
    /// <returns>The summary.</returns>
    internal static ExportPagesSummary Refused(string problem) => new(0, 0, 0, string.Empty, problem);
}
