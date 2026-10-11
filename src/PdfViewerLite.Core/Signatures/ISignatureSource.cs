// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;

namespace PdfViewerLite.Core.Signatures;

/// <summary>Reads a document's digital signatures. Safe to call from any thread.</summary>
public interface ISignatureSource : IDocumentFeature
{
    /// <summary>Gets the number of digital signatures.</summary>
    int SignatureCount { get; }

    /// <summary>Reads every digital signature.</summary>
    /// <returns>The signatures as stored.</returns>
    IReadOnlyList<RawSignature> GetSignatures();
}
