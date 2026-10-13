// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>An entry in the document outline (bookmarks).</summary>
/// <param name="Title">The display title.</param>
/// <param name="Target">The navigation target.</param>
/// <param name="Children">The child entries.</param>
/// <param name="IsOpen">Whether the document requests the entry be expanded.</param>
[DebuggerDisplay("OutlineNode: {Title}")]
public sealed record OutlineNode(string Title, LinkTarget Target, IReadOnlyList<OutlineNode> Children, bool IsOpen)
{
    /// <summary>Gets the entry's object number in the file, which <see cref="IOutlineEditor"/> takes to name the entry; 0 when the entry cannot be edited.</summary>
    public int Id { get; init; }
}
