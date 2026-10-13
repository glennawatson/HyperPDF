// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The outcome of trying a certificate against a CMS recipient list.</summary>
internal enum CmsRecipientOutcome
{
    /// <summary>No recipient in the list is for the certificate.</summary>
    NotForCertificate = 0,

    /// <summary>A recipient for the certificate was decrypted.</summary>
    Opened = 1,

    /// <summary>A recipient is for the certificate, but the certificate has no private key.</summary>
    PrivateKeyMissing = 2,

    /// <summary>A recipient is for the certificate, but its key type or algorithms are not supported.</summary>
    Unsupported = 3,

    /// <summary>A recipient is for the certificate, but its key or content does not decrypt.</summary>
    Damaged = 4,

    /// <summary>The CMS data cannot be read, so it is not known whom it is for.</summary>
    Malformed = 5,
}
