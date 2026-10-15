// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Engine;

/// <summary>Provides IRenderSharpness through the owning document.</summary>
internal sealed class HyperPdfRenderSharpnessService : IRenderSharpness
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfRenderSharpnessService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfRenderSharpnessService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    public RenderSharpness Sharpness { get => HyperPdfDocumentRenderSharpness.GetSharpness(_owner); set => HyperPdfDocumentRenderSharpness.SetSharpness(_owner, value); }
}
