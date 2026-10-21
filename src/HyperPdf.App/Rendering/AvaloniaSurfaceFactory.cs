// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Rendering;

namespace HyperPdf.App.Rendering;

/// <summary>Creates tiles that prepare page recordings on the render worker for graphics-thread replay.</summary>
internal sealed class AvaloniaSurfaceFactory : IOwnedSurfaceFactory
{
    /// <summary>Makes the Avalonia bitmap tile that stands in when graphics cannot be used; one delegate serves every tile.</summary>
    private static readonly Func<int, int, IRenderSurface> SoftwareTiles = CreateSoftware;

    /// <summary>Shared graphics availability for this render hub.</summary>
    private readonly GpuAvailability _availability = new();

    /// <summary>Schedules UI cache recovery after a graphics failure.</summary>
    private readonly Action _recoverSoftware;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaSurfaceFactory"/> class.</summary>
    /// <param name="recoverSoftware">Requests software tiles on the UI thread after graphics failure.</param>
    internal AvaloniaSurfaceFactory(Action recoverSoftware) => _recoverSoftware = recoverSoftware;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IRenderSurface Create(int width, int height) => new GpuPreparedRenderSurface(width, height, _availability, SoftwareTiles, _recoverSoftware);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _availability.Dispose();

    /// <summary>Makes the Avalonia bitmap tile that stands in when graphics cannot be used.</summary>
    /// <param name="width">The tile width.</param>
    /// <param name="height">The tile height.</param>
    /// <returns>The tile.</returns>
    private static AvaloniaRenderSurface CreateSoftware(int width, int height) => new(width, height);
}
