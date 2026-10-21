// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Rendering;

/// <summary>What the UI toolkit supplies to the viewer: where tiles are drawn, how a frame is requested and how pictures are read.</summary>
public interface IRenderHead
{
    /// <summary>Gets the assembly that embeds the third-party notices the Licences window shows.</summary>
    System.Reflection.Assembly NoticesAssembly { get; }

    /// <summary>Creates the factory for the tiles the render workers fill.</summary>
    /// <param name="recoverSoftware">Asks for software tiles on the UI thread after the graphics device is lost.</param>
    /// <returns>The factory; the caller disposes it.</returns>
    IOwnedSurfaceFactory CreateSurfaceFactory(Action recoverSoftware);

    /// <summary>Starts a frame for every open window, even when no page canvas is attached.</summary>
    void InvalidateDisplay();

    /// <summary>Reads a picture of a signature (PNG, JPEG, BMP or WebP) into straight-alpha BGRA pixels.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The image, or <see langword="null"/> when the file cannot be read as a picture.</returns>
    DecodedImage? LoadImage(string path);
}
