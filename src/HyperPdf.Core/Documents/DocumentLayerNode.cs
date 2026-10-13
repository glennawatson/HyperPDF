// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>An entry of the layer panel's tree: a layer, or a label that groups layers and cannot be switched.</summary>
/// <param name="Name">The layer's name, or the label's text.</param>
/// <param name="LayerId">The layer's object number; 0 for a label.</param>
/// <param name="Children">The entries listed under this one.</param>
[DebuggerDisplay("DocumentLayerNode: {Name} id={LayerId} children={Children.Count}")]
public sealed record DocumentLayerNode(string Name, int LayerId, IReadOnlyList<DocumentLayerNode> Children)
{
    /// <summary>Gets a value indicating whether this entry is a label rather than a layer.</summary>
    public bool IsLabel => LayerId == 0;
}
