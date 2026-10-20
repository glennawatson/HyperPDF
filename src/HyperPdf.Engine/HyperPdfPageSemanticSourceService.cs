// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Reading;

namespace HyperPdf.Engine;

/// <summary>Provides IPageSemanticSource through the owning document.</summary>
internal sealed class HyperPdfPageSemanticSourceService : IPageSemanticSource
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPageSemanticSourceService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfPageSemanticSourceService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<PageSemanticItem> GetItems(int pageIndex) => HyperPdfPageSemantics.GetItems(_owner, pageIndex);
}
