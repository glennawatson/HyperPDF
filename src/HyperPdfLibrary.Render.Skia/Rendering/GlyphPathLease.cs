// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Releases only a path that exceeded the recording cache.</summary>
/// <param name="Path">The native glyph outline.</param>
/// <param name="Owns">Whether this draw must dispose it.</param>
internal readonly record struct GlyphPathLease(SKPath Path, bool Owns) : IDisposable
{
    /// <inheritdoc/>
    public void Dispose()
    {
        if (Owns)
        {
            Path.Dispose();
        }
    }
}
