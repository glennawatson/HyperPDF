// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Windowing.Accessibility;

/// <summary>What changed on a node.</summary>
public enum AccessibleChange
{
    /// <summary>The name changed.</summary>
    Name = 0,

    /// <summary>The value changed.</summary>
    Value = 1,

    /// <summary>The focus moved to the node.</summary>
    Focus = 2,

    /// <summary>The states changed.</summary>
    States = 3,

    /// <summary>The text selection in the node changed.</summary>
    Selection = 4,
}
