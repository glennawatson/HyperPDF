// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Identifies a pattern shader within one recording: the pattern's tile or shading, and the matrix it is placed with.</summary>
/// <param name="Source">The pattern's tile or shading, compared by reference.</param>
/// <param name="Local">The matrix from the shape's space to the pattern's.</param>
internal readonly record struct ShaderKey(object Source, SKMatrix Local)
{
    /// <inheritdoc/>
    public bool Equals(ShaderKey other) => ReferenceEquals(Source, other.Source) && Local.Equals(other.Local);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Source), Local);
}
