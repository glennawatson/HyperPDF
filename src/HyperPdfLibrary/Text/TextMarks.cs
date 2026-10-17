// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Text;

/// <summary>
/// The replacement text of the marked-content sequence a run is in (ISO 32000-2, 14.9.4): the /ActualText of the
/// innermost enclosing sequence that has one.
/// </summary>
/// <param name="Id">Numbers the sequence in content order, so every run of one sequence shares it; 0 when no sequence supplies text.</param>
/// <param name="ActualText">The replacement text; empty when there is none.</param>
[DebuggerDisplay("TextMarks: sequence {Id}")]
internal readonly record struct TextMarks(int Id, string ActualText)
{
    /// <summary>Gets the marks of text that no /ActualText covers.</summary>
    internal static TextMarks None { get; } = new(0, string.Empty);
}
