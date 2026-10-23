// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The directions UI Automation moves through the tree of elements.</summary>
internal enum NavigateDirection
{
    /// <summary>To the parent.</summary>
    Parent = 0,

    /// <summary>To the next sibling.</summary>
    NextSibling = 1,

    /// <summary>To the previous sibling.</summary>
    PreviousSibling = 2,

    /// <summary>To the first child.</summary>
    FirstChild = 3,

    /// <summary>To the last child.</summary>
    LastChild = 4,
}
