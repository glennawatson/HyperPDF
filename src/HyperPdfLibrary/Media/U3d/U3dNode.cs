// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>A node of the scene graph.</summary>
/// <param name="Kind">What kind of node it is.</param>
/// <param name="Name">The node's name.</param>
/// <param name="Parents">The names of the node's parents; an empty name is the default root.</param>
/// <param name="Resource">The name of the model, light or view resource the node uses, or an empty string for a group.</param>
[DebuggerDisplay("U3dNode: {Kind} {Name}")]
public sealed record U3dNode(U3dNodeKind Kind, string Name, string[] Parents, string Resource);
