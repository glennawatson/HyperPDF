// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The states of an element that opens and closes.</summary>
internal enum ExpandCollapseState
{
    /// <summary>The element is closed.</summary>
    Collapsed = 0,

    /// <summary>The element is open.</summary>
    Expanded = 1,

    /// <summary>The element is partly open.</summary>
    PartiallyExpanded = 2,

    /// <summary>The element has nothing to open.</summary>
    LeafNode = 3,
}
