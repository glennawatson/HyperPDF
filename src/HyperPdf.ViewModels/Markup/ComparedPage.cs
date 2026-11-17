// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Markup;

/// <summary>How the words of one page differ between the open document and another one.</summary>
/// <param name="Page">The zero based page.</param>
/// <param name="Added">The number of words the other document has that this one does not.</param>
/// <param name="Removed">The number of words this document has that the other one does not.</param>
[DebuggerDisplay("ComparedPage: page {Page + 1}, +{Added} -{Removed}")]
public sealed record ComparedPage(int Page, int Added, int Removed)
{
    /// <summary>Gets the share of the page that looks different between the two documents, from 0 to 1.</summary>
    public double LookChange { get; init; }
}
