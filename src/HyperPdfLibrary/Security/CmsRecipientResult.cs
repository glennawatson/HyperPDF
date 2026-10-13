// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The result of trying a certificate against a CMS recipient list.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Content">The decrypted content when <paramref name="Outcome"/> is <see cref="CmsRecipientOutcome.Opened"/>.</param>
internal readonly record struct CmsRecipientResult(CmsRecipientOutcome Outcome, byte[]? Content)
{
    /// <summary>Gets the result for a list with no recipient for the certificate.</summary>
    internal static CmsRecipientResult NotForCertificate => new(CmsRecipientOutcome.NotForCertificate, null);

    /// <summary>Gets the result for a recipient whose certificate has no private key.</summary>
    internal static CmsRecipientResult PrivateKeyMissing => new(CmsRecipientOutcome.PrivateKeyMissing, null);

    /// <summary>Gets the result for a recipient with an unsupported key type or algorithm.</summary>
    internal static CmsRecipientResult Unsupported => new(CmsRecipientOutcome.Unsupported, null);

    /// <summary>Gets the result for a recipient that does not decrypt.</summary>
    internal static CmsRecipientResult Damaged => new(CmsRecipientOutcome.Damaged, null);

    /// <summary>Gets the result for CMS data that cannot be read.</summary>
    internal static CmsRecipientResult Malformed => new(CmsRecipientOutcome.Malformed, null);

    /// <summary>Creates the result for a decrypted recipient.</summary>
    /// <param name="content">The decrypted content.</param>
    /// <returns>The result.</returns>
    internal static CmsRecipientResult Opened(byte[] content) => new(CmsRecipientOutcome.Opened, content);
}
