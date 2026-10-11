// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>Stores parsed bundled faces.</summary>
[DebuggerDisplay("Bundled faces")]
internal sealed class BundledFaceState
{
    /// <summary>The number of open font faces.</summary>
    internal const int FaceCount = 14;

    /// <summary>Gets the faces by family and style.</summary>
    internal SubstituteFace?[] Faces { get; } = new SubstituteFace?[FaceCount];
}
