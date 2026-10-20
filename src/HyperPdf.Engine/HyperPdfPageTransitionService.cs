// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Presentation;

namespace HyperPdf.Engine;

/// <summary>Provides IPageTransitionSource through the owning document.</summary>
internal sealed class HyperPdfPageTransitionService : IPageTransitionSource
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPageTransitionService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfPageTransitionService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PageTransition? GetTransition(int pageIndex) => HyperPdfDocumentTransitions.GetTransition(_owner, pageIndex);
}
