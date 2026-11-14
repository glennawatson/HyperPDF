// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Windowing.Accessibility;

/// <summary>One node of the accessible tree, in the shape every platform maps to its own API.</summary>
/// <param name="Id">The node id, unique in the window.</param>
/// <param name="ParentId">The parent id, or -1 for the root.</param>
/// <param name="Role">What the node is.</param>
/// <param name="Name">The spoken name.</param>
/// <param name="Value">The current value, or <see langword="null"/>.</param>
/// <param name="Bounds">Where the node is in window device pixels.</param>
/// <param name="States">The node states.</param>
[DebuggerDisplay("AccessibleNode {Id}: {Role} {Name}")]
public readonly record struct AccessibleNode(
    int Id,
    int ParentId,
    AccessibleRole Role,
    string Name,
    string? Value,
    AccessibleBounds Bounds,
    AccessibleStates States);
