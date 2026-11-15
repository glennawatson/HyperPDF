// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;
using HyperPdf.ViewModels.Rendering;

namespace HyperPdf.Viewer;

/// <summary>Makes page tiles with the surface factory of the window's render backend. The backend owns the factory, so disposing this does nothing.</summary>
/// <param name="surfaces">The backend's surface factory.</param>
internal sealed class ViewerSurfaceFactory(IRenderSurfaceFactory surfaces) : IOwnedSurfaceFactory
{
    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IRenderSurface Create(int width, int height) => surfaces.Create(width, height);

    /// <inheritdoc/>
    public void Dispose()
    {
        // The render backend frees its surfaces and factory when the window closes.
    }
}
