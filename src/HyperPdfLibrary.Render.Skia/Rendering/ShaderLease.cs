// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>A pattern shader for one draw: released after the draw only when it was made for that draw alone.</summary>
/// <param name="Shader">The shader, or <see langword="null"/> when the colour is not a pattern.</param>
/// <param name="Owns">Whether this draw releases the shader.</param>
internal readonly record struct ShaderLease(SKShader? Shader, bool Owns) : IDisposable
{
    /// <inheritdoc/>
    public void Dispose()
    {
        if (Owns)
        {
            Shader?.Dispose();
        }
    }
}
