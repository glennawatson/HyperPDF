// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Layers;

/// <summary>
/// An entry of the layer order (ISO 32000-2, 8.11.4.3, Table 99 /Order): a layer, or a text label that groups related
/// layers and cannot be switched.
/// </summary>
/// <param name="Name">The layer's name, or the label's text.</param>
/// <param name="LayerId">The layer's object number; 0 for a label.</param>
/// <param name="Children">The layers listed under this entry.</param>
[DebuggerDisplay("PdfLayerNode: {Name} id={LayerId} children={Children.Length}")]
public sealed record PdfLayerNode(string Name, int LayerId, PdfLayerNode[] Children)
{
    /// <summary>Gets a value indicating whether this entry is a label rather than a layer.</summary>
    public bool IsLabel => LayerId == 0;
}
