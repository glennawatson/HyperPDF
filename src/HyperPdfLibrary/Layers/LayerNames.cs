// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace HyperPdfLibrary.Layers;

/// <summary>Finds a layer's name from its object number.</summary>
/// <param name="Layers">The layers in document order.</param>
/// <param name="SortedIds">The layers' object numbers, ascending.</param>
/// <param name="DocumentPositions">For each entry of <paramref name="SortedIds"/>, the position of that layer in <paramref name="Layers"/>.</param>
[DebuggerDisplay("LayerNames: {Layers.Length} layers")]
internal readonly record struct LayerNames(PdfLayer[] Layers, int[] SortedIds, int[] DocumentPositions)
{
    /// <summary>Gets the name of a layer.</summary>
    /// <param name="id">The group's object number.</param>
    /// <param name="name">Receives the name.</param>
    /// <returns><see langword="true"/> when the document has the layer.</returns>
    internal bool TryGetName(int id, [NotNullWhen(true)] out string? name)
    {
        var found = Array.BinarySearch(SortedIds, id);
        if (found < 0)
        {
            name = null;
            return false;
        }

        name = Layers[DocumentPositions[found]].Name;
        return true;
    }
}
