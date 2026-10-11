// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>
/// A caller-owned drawing target that does not require CPU pixel access. Drawing produces premultiplied sRGB colour;
/// the caller keeps the target and its graphics device alive until submitted work and presentation finish.
/// </summary>
public interface IPdfRenderTarget : IDisposable
{
    /// <summary>Gets the target width in pixels.</summary>
    int Width { get; }

    /// <summary>Gets the target height in pixels.</summary>
    int Height { get; }

    /// <summary>Gets whether the target can accept drawing on the current graphics context.</summary>
    bool IsValid { get; }
}
