// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.ViewModels.Rendering;
using HyperPdf.ViewModels.Services;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Viewer;

/// <summary>The render head of the native viewer: tiles come from the window's render backend and a display refresh is a frame request.</summary>
/// <param name="window">The window the viewer draws into.</param>
[DebuggerDisplay("ViewerRenderHead: {window.Backend.Kind}")]
public sealed class ViewerRenderHead(IPlatformWindow window) : IRenderHead
{
    /// <inheritdoc/>
    public string NoticesFolder => AppContext.BaseDirectory;

    /// <inheritdoc/>
    public IOwnedSurfaceFactory CreateSurfaceFactory(Action recoverSoftware)
    {
        // The backend was chosen when the window opened, and a GPU backend that fails falls back there, so there is no later recovery to ask for.
        _ = recoverSoftware;
        return new ViewerSurfaceFactory(window.Backend.Surfaces);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InvalidateDisplay() => window.RequestFrame();

    /// <inheritdoc/>
    public DecodedImage? LoadImage(string path)
    {
        // Picture decoding for signature marks arrives with the fill and sign panel of the native heads.
        _ = path;
        return null;
    }
}
