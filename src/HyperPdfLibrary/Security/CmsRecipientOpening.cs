// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Security;

/// <summary>
/// Finds the recipient for a certificate in the public-key handler's CMS recipient lists (ISO 32000-2 7.6.5) and decrypts
/// its enveloped content: RSA key transport recipients and ECDH key agreement recipients. Lists that cannot be read are
/// skipped, so one damaged list does not hide a later one for the certificate.
/// </summary>
internal static class CmsRecipientOpening
{
    /// <summary>The rank of a list with no recipient for the certificate.</summary>
    private const int NoMatchRank = 0;

    /// <summary>The rank of a list that cannot be read.</summary>
    private const int MalformedRank = 1;

    /// <summary>The rank of a recipient for the certificate that cannot be opened.</summary>
    private const int MatchedRank = 2;

    /// <summary>Opens the first recipient for the certificate.</summary>
    /// <param name="recipients">The CMS ContentInfo of each recipient list, in order.</param>
    /// <param name="certificate">The certificate.</param>
    /// <returns>
    /// The content of the first recipient that opens; otherwise the first failure of a recipient for the certificate, a
    /// malformed result when a list cannot be read, or a result saying no recipient is for the certificate.
    /// </returns>
    internal static CmsRecipientResult Open(byte[][] recipients, X509Certificate2 certificate)
    {
        var identity = CmsRecipientMatching.Identify(certificate);
        var result = CmsRecipientResult.NotForCertificate;
        foreach (var recipient in recipients)
        {
            result = Prefer(result, OpenList(recipient, identity));
            if (result.Outcome == CmsRecipientOutcome.Opened)
            {
                return result;
            }
        }

        return result;
    }

    /// <summary>Opens a recipient list: a ContentInfo holding EnvelopedData (RFC 5652 3 and 6.1).</summary>
    /// <param name="recipient">The ContentInfo DER.</param>
    /// <param name="identity">The certificate.</param>
    /// <returns>The outcome.</returns>
    private static CmsRecipientResult OpenList(byte[] recipient, CmsRecipientIdentity identity)
    {
        try
        {
            ReadOnlySpan<byte> source = recipient;
            var contentInfo = CmsDer.ReadContents(ref source, Asn1Tag.Sequence);
            if (!CmsDer.ReadContents(ref contentInfo, Asn1Tag.ObjectIdentifier).SequenceEqual(CmsOids.EnvelopedData))
            {
                return CmsRecipientResult.Malformed;
            }

            var content = CmsDer.ReadContents(ref contentInfo, CmsDer.Constructed0);
            var envelopedData = CmsDer.ReadContents(ref content, Asn1Tag.Sequence);
            _ = CmsDer.ReadContents(ref envelopedData, Asn1Tag.Integer);
            if (CmsDer.StartsWith(envelopedData, CmsDer.Constructed0))
            {
                // The originator's certificates and CRLs play no part in decryption.
                _ = CmsDer.ReadEncoded(ref envelopedData);
            }

            var infos = CmsDer.ReadContents(ref envelopedData, Asn1Tag.SetOf);
            var encryptedContentInfo = CmsDer.ReadContents(ref envelopedData, Asn1Tag.Sequence);
            return OpenRecipientInfos(recipient, infos, encryptedContentInfo, identity);
        }
        catch (AsnContentException)
        {
            return CmsRecipientResult.Malformed;
        }
    }

    /// <summary>Tries each RecipientInfo; KEK, password and other recipient kinds cannot name a certificate and are skipped.</summary>
    /// <param name="recipient">The ContentInfo DER.</param>
    /// <param name="infos">The RecipientInfos' contents.</param>
    /// <param name="encryptedContentInfo">The EncryptedContentInfo's contents.</param>
    /// <param name="identity">The certificate.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="AsnContentException">A recipient info is damaged.</exception>
    private static CmsRecipientResult OpenRecipientInfos(byte[] recipient, ReadOnlySpan<byte> infos, ReadOnlySpan<byte> encryptedContentInfo, CmsRecipientIdentity identity)
    {
        var result = CmsRecipientResult.NotForCertificate;
        while (!infos.IsEmpty && result.Outcome != CmsRecipientOutcome.Opened)
        {
            var tag = CmsDer.PeekTag(infos);
            if (tag == Asn1Tag.Sequence)
            {
                result = Prefer(result, CmsKeyTransport.Open(recipient, CmsDer.ReadContents(ref infos, Asn1Tag.Sequence), identity));
            }
            else if (tag == CmsDer.Constructed1)
            {
                result = Prefer(result, CmsKeyAgreement.Open(CmsDer.ReadContents(ref infos, CmsDer.Constructed1), encryptedContentInfo, identity));
            }
            else
            {
                _ = CmsDer.ReadEncoded(ref infos);
            }
        }

        return result;
    }

    /// <summary>Keeps an opened result, else the first failure of a recipient for the certificate, else a malformed list.</summary>
    /// <param name="current">The result so far.</param>
    /// <param name="next">The next result.</param>
    /// <returns>The result to keep.</returns>
    private static CmsRecipientResult Prefer(CmsRecipientResult current, CmsRecipientResult next) =>
        next.Outcome == CmsRecipientOutcome.Opened || Rank(next.Outcome) > Rank(current.Outcome) ? next : current;

    /// <summary>Ranks an outcome for reporting when nothing opens.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The rank; higher is reported first.</returns>
    private static int Rank(CmsRecipientOutcome outcome) => outcome switch
    {
        CmsRecipientOutcome.NotForCertificate => NoMatchRank,
        CmsRecipientOutcome.Malformed => MalformedRank,
        _ => MatchedRank,
    };
}
