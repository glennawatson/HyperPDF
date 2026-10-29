// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using HyperPdf.App.Services;

namespace HyperPdf.App.Rendering;

/// <summary>The Avalonia side of rendering: GPU or software tiles, frame requests and picture decoding.</summary>
[DebuggerDisplay("AvaloniaRenderHead")]
public sealed class AvaloniaRenderHead : IRenderHead
{
    /// <inheritdoc/>
    public string NoticesFolder => FindNoticesFolder(AppContext.BaseDirectory);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IOwnedSurfaceFactory CreateSurfaceFactory(Action recoverSoftware) => new AvaloniaSurfaceFactory(recoverSoftware);

    /// <inheritdoc/>
    public void InvalidateDisplay()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var window in desktop.Windows)
            {
                window.InvalidateVisual();
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DecodedImage? LoadImage(string path) => SignatureImageFile.Load(path);

    /// <summary>Picks the folder that holds the notices file.</summary>
    /// <param name="baseDirectory">The folder the app runs from.</param>
    /// <returns>The macOS bundle's Contents/Resources when the file is there, otherwise the base folder.</returns>
    private static string FindNoticesFolder(string baseDirectory)
    {
        // A macOS bundle keeps non-code files in Contents/Resources, because codesign --strict rejects them in Contents/MacOS.
        var resources = Path.GetFullPath(Path.Combine(baseDirectory, "..", "Resources"));
        return File.Exists(Path.Combine(resources, "THIRD-PARTY-NOTICES.md")) ? resources : baseDirectory;
    }
}
