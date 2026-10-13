// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Cms;

namespace HyperPdfLibrary.Security;

/// <summary>The plain sentences a PDF MAC check gives, and the result a token status maps to.</summary>
internal static class PdfMacMessages
{
    /// <summary>Gets the message of a revision with no PDF MAC that does not need one.</summary>
    internal static string NotNeeded => "The revision has no PDF MAC, and the permissions do not require one.";

    /// <summary>Gets the message of a revision whose PDF MAC matches.</summary>
    internal static string Matches => "The PDF MAC was made with this document's encryption key and matches the revision's bytes.";

    /// <summary>Gets the message of a revision the permissions require a PDF MAC for, which has none.</summary>
    internal static string Required => "The permissions require a PDF MAC, but this revision has none. The revision may have been added by a program that does not keep it.";

    /// <summary>Gets the message of a revision whose bytes no longer match its PDF MAC.</summary>
    internal static string BytesChanged => "The revision's bytes do not match its PDF MAC. They were changed after the PDF MAC was made.";

    /// <summary>Gets the message of a PDF MAC whose byte range stops short of the end of its revision.</summary>
    internal static string Stale => "The PDF MAC does not cover the whole revision. It is out of date: another program changed the file without updating it.";

    /// <summary>Gets the message of a PDF MAC whose location or byte range is not as the standard requires.</summary>
    internal static string BadLayout => "The PDF MAC entry does not follow ISO/TS 32004: its location, byte range or hex string is wrong.";

    /// <summary>Gets the message of an Encrypt dictionary that cannot support a PDF MAC.</summary>
    internal static string BadEncrypt => "The PDF MAC cannot be checked: the encryption dictionary has no 32-byte /KDFSalt, or its version is below 5.";

    /// <summary>Gets the message of a PDF MAC location this reader does not know.</summary>
    internal static string UnknownLocation => "The PDF MAC is stored in a way this reader does not know, so it was not checked.";

    /// <summary>Gets the message of a signature that should carry a PDF MAC and does not.</summary>
    internal static string NoSignatureMac => "The trailer says the PDF MAC is attached to a signature, but the signature has none.";

    /// <summary>Gets the message of a signature whose PDF MAC has no signature digest.</summary>
    internal static string NoSignatureDigest => "The PDF MAC on this signature does not protect the signature value, as the standard requires.";

    /// <summary>Gets the message of a PDF MAC on a signature whose value was changed.</summary>
    internal static string SignatureChanged => "The signature value does not match the PDF MAC that protects it. The signature was changed.";

    /// <summary>Gets the message of a standalone PDF MAC that also protects a signature.</summary>
    internal static string UnexpectedSignatureDigest => "The standalone PDF MAC has a signature digest, which the standard only allows on a PDF MAC attached to a signature.";

    /// <summary>Gets the message for a failed token.</summary>
    /// <param name="status">The token status.</param>
    /// <param name="digest">The token's digest.</param>
    /// <returns>The sentence.</returns>
    internal static string ForToken(PdfMacTokenStatus status, CmsDigestAlgorithm digest) => status switch
    {
        PdfMacTokenStatus.Malformed => "The PDF MAC token is damaged or does not follow the CMS structure ISO/TS 32004 requires.",
        PdfMacTokenStatus.UnsupportedAlgorithm => "The PDF MAC token uses a key derivation, key wrap or MAC method that this reader does not check.",
        PdfMacTokenStatus.DigestNotAllowed => "The PDF MAC token uses a digest that ISO/TS 32004 does not allow.",
        PdfMacTokenStatus.DigestUnavailable => $"The PDF MAC token uses {CmsDigests.Name(digest)}, which this computer's cryptography library does not provide, so it was not checked.",
        PdfMacTokenStatus.KeyUnwrapFailed => "The PDF MAC was not made with this document's encryption key, or its wrapped key was changed.",
        PdfMacTokenStatus.ContentDigestMismatch or PdfMacTokenStatus.MacMismatch => "The PDF MAC token does not match: it was changed or damaged.",
        _ => Matches,
    };

    /// <summary>Gets the status a failed token gives the revision.</summary>
    /// <param name="status">The token status.</param>
    /// <returns>Unsupported when the token cannot be checked here, otherwise invalid.</returns>
    internal static PdfMacStatus ForTokenStatus(PdfMacTokenStatus status) => status switch
    {
        PdfMacTokenStatus.Valid => PdfMacStatus.Valid,
        PdfMacTokenStatus.UnsupportedAlgorithm or PdfMacTokenStatus.DigestUnavailable => PdfMacStatus.Unsupported,
        _ => PdfMacStatus.Invalid,
    };
}
