// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>The kind of signature container to write (ISO 32000-2, 12.8.3).</summary>
public enum SigningFormat
{
    /// <summary>The format the field's seed values allow, falling back to <see cref="Pkcs7Detached"/>.</summary>
    Automatic = 0,

    /// <summary>A detached PKCS #7 signature (adbe.pkcs7.detached), which every PDF reader can check.</summary>
    Pkcs7Detached = 1,

    /// <summary>A detached CAdES signature (ETSI.CAdES.detached), the PAdES baseline format.</summary>
    CadesDetached = 2,
}
