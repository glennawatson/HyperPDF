// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Layers;

/// <summary>A layer (optional content group).</summary>
/// <param name="Id">The group's object number.</param>
/// <param name="Name">The group's name.</param>
/// <param name="IsVisible">Whether it is shown, before any zoom limit.</param>
/// <param name="IsLocked">Whether the configuration locks it, so the user interface cannot change it.</param>
/// <param name="IsPrinted">Whether it prints.</param>
/// <param name="IsExported">Whether it is kept when the document is saved to a format without optional content.</param>
/// <param name="ZoomMin">The magnification from which an automatic state shows it; 0 when unlimited.</param>
/// <param name="ZoomMax">The magnification below which an automatic state shows it; infinity when unlimited.</param>
[DebuggerDisplay("PdfLayer: {Name} visible={IsVisible}")]
public readonly record struct PdfLayer(int Id, string Name, bool IsVisible, bool IsLocked, bool IsPrinted, bool IsExported, double ZoomMin, double ZoomMax)
{
    /// <summary>Initializes a new instance of the <see cref="PdfLayer"/> struct for an unlocked layer without zoom limits.</summary>
    /// <param name="id">The group's object number.</param>
    /// <param name="name">The group's name.</param>
    /// <param name="isVisible">Whether it is shown.</param>
    public PdfLayer(int id, string name, bool isVisible)
        : this(id, name, isVisible, false, true, true, 0, double.PositiveInfinity)
    {
    }

    /// <summary>Gets a value indicating whether a zoom range limits when the layer is shown.</summary>
    public bool HasZoomLimit => ZoomMin > 0 || !double.IsPositiveInfinity(ZoomMax);
}
