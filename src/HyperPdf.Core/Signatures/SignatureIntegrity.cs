// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Signatures;

/// <summary>Whether the signed content is what the signer signed.</summary>
public enum SignatureIntegrity
{
    /// <summary>The signature could not be checked, for example an unsupported format.</summary>
    Unknown = 0,

    /// <summary>The signature matches and covers the whole document.</summary>
    Intact = 1,

    /// <summary>The signature matches what was signed, but the document was changed afterwards (for example, filled in or annotated).</summary>
    ChangedAfterSigning = 2,

    /// <summary>The signature does not match: the signed content was altered or the signature is damaged.</summary>
    Invalid = 3,

    /// <summary>
    /// The signature uses a digest or signature algorithm this computer cannot check, such as SHA-3 where the system's
    /// cryptography library lacks it; it is neither confirmed nor rejected.
    /// </summary>
    Unsupported = 4,

    /// <summary>The signature uses a digest PDF does not allow, such as SHAKE128, so it is not valid.</summary>
    DigestNotAllowed = 5,

    /// <summary>The signature pairs an elliptic curve with a digest ISO/TS 32002 does not permit, so it is not valid.</summary>
    CurveDigestNotAllowed = 6,

    /// <summary>The signer's certificate gives its elliptic curve as explicit parameters, not a named curve, so the signature is not valid.</summary>
    UnnamedCurve = 7,

    /// <summary>The signature algorithm hashes the signed attributes with a different digest from the content's, so the signature is not valid.</summary>
    DigestMismatch = 8,
}
