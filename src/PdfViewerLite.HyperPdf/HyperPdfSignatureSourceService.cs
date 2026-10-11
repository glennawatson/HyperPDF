// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Signatures;

namespace PdfViewerLite.HyperPdf;

/// <summary>Provides ISignatureSource through the owning document.</summary>
internal sealed class HyperPdfSignatureSourceService : ISignatureSource
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfSignatureSourceService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfSignatureSourceService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    public int SignatureCount { get => HyperPdfDocumentSignatures.GetSignatureCount(_owner); }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<RawSignature> GetSignatures() => HyperPdfDocumentSignatures.GetSignatures(_owner);
}
