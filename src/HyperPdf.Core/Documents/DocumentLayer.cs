// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A layer (optional content group) that can be shown or hidden.</summary>
/// <param name="Id">The layer's object number in the file.</param>
/// <param name="Name">The layer's name.</param>
/// <param name="IsVisible">Whether it is shown.</param>
/// <param name="IsLocked">Whether the file locks it, so the user cannot change it.</param>
/// <param name="IsPrinted">Whether it prints.</param>
/// <param name="IsExported">Whether it is kept when the document is saved to a format without layers.</param>
/// <param name="ZoomMin">The magnification from which the file shows it automatically; 0 when unlimited.</param>
/// <param name="ZoomMax">The magnification below which the file shows it automatically; infinity when unlimited.</param>
[DebuggerDisplay("DocumentLayer: {Name} visible={IsVisible}")]
public sealed record DocumentLayer(int Id, string Name, bool IsVisible, bool IsLocked, bool IsPrinted, bool IsExported, double ZoomMin, double ZoomMax)
{
    /// <summary>Initializes a new instance of the <see cref="DocumentLayer"/> class for an unlocked layer without zoom limits.</summary>
    /// <param name="id">The layer's object number in the file.</param>
    /// <param name="name">The layer's name.</param>
    /// <param name="isVisible">Whether it is shown.</param>
    public DocumentLayer(int id, string name, bool isVisible)
        : this(id, name, isVisible, false, true, true, 0, double.PositiveInfinity)
    {
    }

    /// <summary>Gets a value indicating whether the file shows the layer only within a range of magnifications.</summary>
    public bool HasZoomLimit => ZoomMin > 0 || !double.IsPositiveInfinity(ZoomMax);
}
