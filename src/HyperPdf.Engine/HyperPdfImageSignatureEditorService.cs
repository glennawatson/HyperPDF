// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Engine;

/// <summary>Provides IImageSignatureEditor through the owning document.</summary>
internal sealed class HyperPdfImageSignatureEditorService : IImageSignatureEditor
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfImageSignatureEditorService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfImageSignatureEditorService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AddImageSignature(int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height) => HyperPdfDocumentImageSignatures.AddImageSignature(
            _owner,
            pageIndex,
            bounds,
            pixels,
            width,
            height);
}
