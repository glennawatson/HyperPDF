// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;

namespace HyperPdf.Engine;

/// <summary>Provides IDocumentXmpSource through the owning document.</summary>
internal sealed class HyperPdfDocumentXmpSourceService : IDocumentXmpSource
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfDocumentXmpSourceService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfDocumentXmpSourceService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<DocumentXmpProperty> GetProperties() => HyperPdfDocumentXmp.GetProperties(_owner);
}
