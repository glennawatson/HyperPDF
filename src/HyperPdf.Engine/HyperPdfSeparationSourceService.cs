// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;

namespace HyperPdf.Engine;

/// <summary>Provides ISeparationSource through the owning document.</summary>
internal sealed class HyperPdfSeparationSourceService : ISeparationSource
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfSeparationSourceService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfSeparationSourceService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<DocumentSeparations> GetSeparationsAsync(CancellationToken cancellationToken) => HyperPdfSeparations.GetSeparationsAsync(_owner, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DocumentSeparations GetSeparations() => HyperPdfSeparations.GetSeparations(_owner);
}
