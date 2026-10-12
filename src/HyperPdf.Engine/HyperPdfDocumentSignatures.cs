// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Signatures;
using HyperPdfLibrary.Document;

namespace HyperPdf.Engine;

/// <summary>Implements DocumentSignatures over the document's owned state.</summary>
internal static class HyperPdfDocumentSignatures
{
    /// <summary>Gets SignatureCount.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The current value.</returns>
    internal static int GetSignatureCount(HyperPdfDocument self) => self.IsDisposed ? 0 : PdfDocumentAttachments.GetSignatures(self.Document).Count;

    /// <summary>Reads every digital signature.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The signatures as stored.</returns>
    internal static IReadOnlyList<RawSignature> GetSignatures(HyperPdfDocument self)
    {
        if (self.IsDisposed)
        {
            return [];
        }

        if (Volatile.Read(ref self.Signatures) is { } cached)
        {
            return cached;
        }

        var source = PdfDocumentAttachments.GetSignatures(self.Document);
        var signatures = new RawSignature[source.Count];
        for (var i = 0; i < signatures.Length; i++)
        {
            var field = source[i];
            signatures[i] = new(field.Index, field.Contents, field.ByteRange, field.SubFilter, field.Reason, field.SigningTime);
        }

        Volatile.Write(ref self.Signatures, signatures);
        return signatures;
    }
}
