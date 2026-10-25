// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Accessibility;

/// <summary>What changed about an element.</summary>
public enum AccessibleChangeKind
{
    /// <summary>An element appeared.</summary>
    Added = 0,

    /// <summary>An element went away.</summary>
    Removed = 1,

    /// <summary>The element's name changed.</summary>
    Name = 2,

    /// <summary>The element's description changed.</summary>
    Description = 3,

    /// <summary>The element's states changed.</summary>
    States = 4,

    /// <summary>The element's children changed.</summary>
    Children = 5,

    /// <summary>The element's text changed.</summary>
    Text = 6,

    /// <summary>The element's caret moved.</summary>
    Caret = 7,

    /// <summary>The keyboard focus moved to the element.</summary>
    Focus = 8,

    /// <summary>The element's bounds changed.</summary>
    Bounds = 9,
}
