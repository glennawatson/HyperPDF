// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Accessibility;

/// <summary>One change to the tree, for a platform bridge to pass on to the screen reader.</summary>
/// <param name="Kind">What changed.</param>
/// <param name="NodeId">The number of the element.</param>
/// <param name="Previous">The element before the change, or <see langword="null"/> when it was added.</param>
/// <param name="Current">The element after the change, or <see langword="null"/> when it was removed.</param>
[DebuggerDisplay("AccessibleChange: {Kind} {NodeId}")]
public sealed record AccessibleChange(AccessibleChangeKind Kind, int NodeId, AccessibleNode? Previous, AccessibleNode? Current);
