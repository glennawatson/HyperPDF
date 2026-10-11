// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Rendering;

namespace PdfViewerLite.App.Rendering;

/// <summary>Creates tiles that prepare page recordings on the render worker for graphics-thread replay.</summary>
internal sealed class AvaloniaSurfaceFactory : IRenderSurfaceFactory
{
    /// <summary>Shared graphics availability for this render hub.</summary>
    private readonly GpuAvailability _availability = new();

    /// <summary>Schedules UI cache recovery after a graphics failure.</summary>
    private readonly Action _recoverSoftware;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaSurfaceFactory"/> class.</summary>
    /// <param name="recoverSoftware">Requests software tiles on the UI thread after graphics failure.</param>
    internal AvaloniaSurfaceFactory(Action recoverSoftware) => _recoverSoftware = recoverSoftware;

    /// <summary>Gets whether the graphics path has failed for this render hub.</summary>
    internal bool IsGpuUnavailable => _availability.IsUnavailable;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IRenderSurface Create(int width, int height) => new GpuPreparedRenderSurface(width, height, _availability, _recoverSoftware);
}
