// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>A separation group being formed: the page whose <c>/Pages</c> array started it, and the pages that joined.</summary>
/// <param name="creator">The page whose array started the group.</param>
[DebuggerDisplay("SeparationGroupBuild: {Members.Count} pages")]
internal sealed class SeparationGroupBuild(SeparationEntry creator)
{
    /// <summary>Gets the page whose array started the group; every other member's array must match it.</summary>
    internal SeparationEntry Creator { get; } = creator;

    /// <summary>Gets the pages that joined, in the order the creator's array lists them.</summary>
    internal List<SeparationEntry> Members { get; } = [];
}
