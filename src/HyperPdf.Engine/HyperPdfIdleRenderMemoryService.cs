// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Engine;

/// <summary>Provides IIdleRenderMemory through the owning document.</summary>
internal sealed class HyperPdfIdleRenderMemoryService : IIdleRenderMemory
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfIdleRenderMemoryService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfIdleRenderMemoryService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Release() => Volatile.Read(ref _owner.RendererState)?.ReleaseRecordedPages();
}
