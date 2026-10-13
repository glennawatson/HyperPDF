// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Cms;

/// <summary>The result of checking a CMS signer's signature.</summary>
internal enum CmsSignatureStatus
{
    /// <summary>The signature and its message digest are good.</summary>
    Valid = 0,

    /// <summary>The signature or its message digest does not match, or the signer cannot be read.</summary>
    Invalid = 1,

    /// <summary>The digest or signature algorithm cannot be checked here.</summary>
    Unsupported = 2,

    /// <summary>The signer's curve and digest are not a pair ISO/TS 32002:2022 permits (Tables 3 and 4).</summary>
    CurveDigestNotAllowed = 3,

    /// <summary>The signer's certificate gives its curve as explicit parameters instead of a named curve (ISO/TS 32002:2022, 5.1.3).</summary>
    UnnamedCurve = 4,

    /// <summary>The signature algorithm hashes the signed attributes with a different digest from the content (ISO/TS 32002:2022, 5.1.4).</summary>
    DigestMismatch = 5,
}
