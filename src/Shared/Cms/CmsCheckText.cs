// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Cms;

/// <summary>Explains in plain words why a signature or timestamp could not be checked.</summary>
internal static class CmsCheckText
{
    /// <summary>Explains a digest that is not available or not allowed.</summary>
    /// <param name="digest">The digest.</param>
    /// <param name="status">Its status.</param>
    /// <param name="oid">The digest OID, shown when the digest is unknown.</param>
    /// <returns>The explanation, or an empty string for an available digest.</returns>
    internal static string Describe(CmsDigestAlgorithm digest, CmsDigestStatus status, string? oid) => status switch
    {
        CmsDigestStatus.NotAvailable => $"This computer cannot compute the {CmsDigests.Name(digest)} digest the signature uses, so the signature could not be checked.",
        CmsDigestStatus.NotAllowed when digest == CmsDigestAlgorithm.Shake256 =>
            "The signature uses SHAKE256 with an output length or parameters PDF does not allow; only the 512-bit form is allowed. The signature is not valid.",
        CmsDigestStatus.NotAllowed => $"The signature uses {CmsDigests.Name(digest)}, which PDF does not allow. The signature is not valid.",
        CmsDigestStatus.Unknown => $"The signature uses a digest that is not supported ({oid}), so the signature could not be checked.",
        _ => string.Empty,
    };

    /// <summary>Explains a signature algorithm that cannot be checked.</summary>
    /// <param name="digest">The digest it is used with.</param>
    /// <returns>The explanation.</returns>
    internal static string UnsupportedSignature(CmsDigestAlgorithm digest) =>
        $"The signature uses a {CmsDigests.Name(digest)} signature algorithm or elliptic curve that cannot be checked here, "
        + "such as a Brainpool curve on a computer whose cryptography library lacks it, so the signature could not be checked.";

    /// <summary>Explains a signer that breaks an ISO/TS 32002 rule, so its signature is not valid.</summary>
    /// <param name="status">The rule broken.</param>
    /// <param name="digest">The signer's digest.</param>
    /// <returns>The explanation, or an empty string for any other status.</returns>
    internal static string Rule(CmsSignatureStatus status, CmsDigestAlgorithm digest) => status switch
    {
        CmsSignatureStatus.CurveDigestNotAllowed =>
            $"The signature pairs its elliptic curve with {CmsDigests.Name(digest)}, a combination PDF does not allow (ISO/TS 32002, Tables 3 and 4). The signature is not valid.",
        CmsSignatureStatus.UnnamedCurve =>
            "The signer's certificate gives its elliptic curve as explicit parameters instead of a named curve, which PDF does not allow (ISO/TS 32002, 5.1.3). The signature is not valid.",
        CmsSignatureStatus.DigestMismatch =>
            "The signature algorithm hashes the signed attributes with a different digest from the one used for the content, "
            + "which PDF does not allow (ISO/TS 32002, 5.1.4). The signature is not valid.",
        _ => string.Empty,
    };

    /// <summary>Determines whether a status means the signer broke an ISO/TS 32002 rule.</summary>
    /// <param name="status">The status.</param>
    /// <returns><see langword="true"/> for a disallowed pair, an unnamed curve or a digest mismatch.</returns>
    internal static bool IsRuleViolation(CmsSignatureStatus status) =>
        status is CmsSignatureStatus.CurveDigestNotAllowed or CmsSignatureStatus.UnnamedCurve or CmsSignatureStatus.DigestMismatch;
}
