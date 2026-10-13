// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Identifies a pattern paint: the tiling cell or shading it paints with, by identity, and the matrix that places it. A
/// cell or shading is shared, so the same pattern selected again at the same place reuses its paint.
/// </summary>
/// <param name="Source">The recorded cell or the shading.</param>
/// <param name="Matrix">The matrix from pattern space to the selecting stream's default space.</param>
[DebuggerDisplay("PatternPaintKey: {Matrix}")]
internal readonly record struct PatternPaintKey(object Source, Matrix3x2 Matrix)
{
    /// <summary>Determines whether another key names the same source object and matrix.</summary>
    /// <param name="other">The other key.</param>
    /// <returns><see langword="true"/> when the source is the same object and the matrices are equal.</returns>
    public bool Equals(PatternPaintKey other) => ReferenceEquals(Source, other.Source) && Matrix == other.Matrix;

    /// <summary>Gets a hash code from the source's identity and the matrix.</summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(Source), Matrix);
}
