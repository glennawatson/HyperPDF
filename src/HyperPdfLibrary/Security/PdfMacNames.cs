// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Security;

/// <summary>The names ISO/TS 32004:2024 adds, interned in one document's name table.</summary>
/// <param name="AuthCode">The trailer's <c>/AuthCode</c>.</param>
/// <param name="KdfSalt">The Encrypt dictionary's <c>/KDFSalt</c>.</param>
/// <param name="MacLocation">The <c>/MACLocation</c> entry.</param>
/// <param name="Mac">The <c>/MAC</c> entry.</param>
/// <param name="SigObjRef">The <c>/SigObjRef</c> entry.</param>
/// <param name="Standalone">The <c>/Standalone</c> location.</param>
/// <param name="AttachedToSig">The <c>/AttachedToSig</c> location.</param>
[DebuggerDisplay("PdfMacNames")]
internal readonly record struct PdfMacNames(PdfName AuthCode, PdfName KdfSalt, PdfName MacLocation, PdfName Mac, PdfName SigObjRef, PdfName Standalone, PdfName AttachedToSig)
{
    /// <summary>Interns the names.</summary>
    /// <param name="names">The document's name table.</param>
    /// <returns>The names.</returns>
    internal static PdfMacNames From(PdfNameTable names) => new(
        names.Intern("AuthCode"u8),
        names.Intern("KDFSalt"u8),
        names.Intern("MACLocation"u8),
        names.Intern("MAC"u8),
        names.Intern("SigObjRef"u8),
        names.Intern("Standalone"u8),
        names.Intern("AttachedToSig"u8));
}
