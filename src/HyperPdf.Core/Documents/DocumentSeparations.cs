// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>The printed pages of a preseparated document and how many problems its separation details have.</summary>
/// <param name="Groups">The printed pages, in order; empty for a document of ordinary pages.</param>
/// <param name="ProblemCount">The number of problems found in the separation details.</param>
[DebuggerDisplay("DocumentSeparations: {Groups.Length} printed pages")]
public sealed record DocumentSeparations(SeparationGroup[] Groups, int ProblemCount)
{
    /// <summary>Gets the result for a document with no separation pages.</summary>
    public static DocumentSeparations Empty { get; } = new([], 0);
}
