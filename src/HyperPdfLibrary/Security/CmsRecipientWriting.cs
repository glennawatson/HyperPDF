// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Security;

/// <summary>Parts of a CMS recipient that key transport and key agreement share.</summary>
internal static class CmsRecipientWriting
{
    /// <summary>Writes an IssuerAndSerialNumber (RFC 5652 10.2.4) that names the certificate: its issuer name as encoded and its serial number.</summary>
    /// <param name="writer">The DER writer.</param>
    /// <param name="certificate">The certificate.</param>
    internal static void WriteIssuerAndSerial(AsnWriter writer, X509Certificate2 certificate)
    {
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(certificate.IssuerName.RawData);
            writer.WriteInteger(certificate.SerialNumberBytes.Span);
        }
    }
}
