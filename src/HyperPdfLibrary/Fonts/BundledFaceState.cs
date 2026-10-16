// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Fonts;

/// <summary>Stores parsed bundled faces.</summary>
[DebuggerDisplay("Bundled faces")]
internal sealed class BundledFaceState
{
    /// <summary>The number of open font faces.</summary>
    internal const int FaceCount = 15;

    /// <summary>The substitute face slots.</summary>
    private SubstituteFace?[]? _faces;

    /// <summary>The decoded program slots.</summary>
    private byte[]?[]? _decoded;

    /// <summary>The parsed program slots.</summary>
    private BundledProgram?[]? _programs;

    /// <summary>The embeddable source slots.</summary>
    private PdfFontOpenResult?[]? _opened;

    /// <summary>Gets the faces by family and style.</summary>
    internal SubstituteFace?[] Faces => GetSlots(ref _faces);

    /// <summary>Gets decoded font programs owned by these resources.</summary>
    internal byte[]?[] Decoded => GetSlots(ref _decoded);

    /// <summary>Gets parsed programs owned by these resources.</summary>
    internal BundledProgram?[] ParsedPrograms => GetSlots(ref _programs);

    /// <summary>Gets embeddable font sources owned by these resources.</summary>
    internal PdfFontOpenResult?[] Opened => GetSlots(ref _opened);

    /// <summary>Creates slots only when the corresponding font operation needs them.</summary>
    /// <typeparam name="T">The cached resource type.</typeparam>
    /// <param name="slots">The owner's slots.</param>
    /// <returns>The published slots.</returns>
    private static T?[] GetSlots<T>(ref T?[]? slots)
        where T : class
    {
        if (Volatile.Read(ref slots) is { } existing)
        {
            return existing;
        }

        var created = new T?[FaceCount];
        return Interlocked.CompareExchange(ref slots, created, null) ?? created;
    }
}
