// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Security;

/// <summary>The issuer and serial number a CMS recipient can name a certificate by (RFC 5652 6.2.1 and 6.2.2), read once per open.</summary>
/// <param name="Certificate">The certificate.</param>
/// <param name="Issuer">The DER of the certificate's issuer name.</param>
/// <param name="SerialNumber">The contents of the certificate's serial number INTEGER.</param>
internal sealed record CmsRecipientIdentity(X509Certificate2 Certificate, ReadOnlyMemory<byte> Issuer, ReadOnlyMemory<byte> SerialNumber);
