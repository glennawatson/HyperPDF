// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Flattening;

/// <summary>What a flattened copy flattens, which look it keeps and whether it turns rotated pages upright.</summary>
/// <param name="Scope">Which parts of the document become plain page content.</param>
/// <param name="Look">Which look of the page is kept.</param>
/// <param name="TurnPagesUpright">Whether pages that are shown turned are saved upright, with the turn built into their content.</param>
[DebuggerDisplay("FlattenCopyOptions: {Scope}, {Look}, upright {TurnPagesUpright}")]
public sealed record FlattenCopyOptions(FlattenCopyScope Scope, FlattenCopyLook Look, bool TurnPagesUpright)
{
    /// <summary>Gets the options a window starts with: form fields and markup as shown on screen, pages left as they are.</summary>
    public static FlattenCopyOptions Default { get; } = new(FlattenCopyScope.EverythingButLinks, FlattenCopyLook.Screen, false);
}
