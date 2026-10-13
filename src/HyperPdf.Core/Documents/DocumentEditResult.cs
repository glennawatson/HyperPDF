// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>The outcome of an edit of the document's bookmarks or attached files.</summary>
/// <param name="Succeeded">Whether the document changed.</param>
/// <param name="Problem">When the edit was refused, why, in words for the reader; otherwise <see langword="null"/>.</param>
[DebuggerDisplay("DocumentEditResult: {Succeeded} {Problem}")]
public sealed record DocumentEditResult(bool Succeeded, string? Problem)
{
    /// <summary>Gets the result of an edit that changed the document.</summary>
    public static DocumentEditResult Done { get; } = new(true, null);

    /// <summary>Makes the result of an edit that was refused.</summary>
    /// <param name="problem">Why, in words for the reader.</param>
    /// <returns>The result.</returns>
    public static DocumentEditResult Refused(string problem) => new(false, problem);
}
