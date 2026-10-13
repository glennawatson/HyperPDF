// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Cms;

/// <summary>Whether a digest algorithm can be used to check a signature here.</summary>
internal enum CmsDigestStatus
{
    /// <summary>The digest is allowed and this computer can compute it.</summary>
    Available = 0,

    /// <summary>The digest is allowed, but this computer's cryptography library cannot compute it.</summary>
    NotAvailable = 1,

    /// <summary>
    /// The digest is one PDF does not allow: SHAKE128, or SHAKE256 with an output other than 512 bits or with
    /// parameters RFC 8702 and RFC 8419 forbid (ISO/TS 32001:2022, 5.1.4).
    /// </summary>
    NotAllowed = 2,

    /// <summary>The digest is not one this code knows.</summary>
    Unknown = 3,
}
