// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The plain-language reasons for refusing to change a document's protection.</summary>
internal static class PdfProtectionMessages
{
    /// <summary>Why a signed document is not rewritten.</summary>
    internal const string Signed =
        "This document is signed. Changing its protection rewrites the whole file, so its signatures would no longer match and would show as broken. "
        + "Remove the signatures first, or leave the file as it is.";

    /// <summary>Why a PDF/A document is not encrypted.</summary>
    internal const string PdfA =
        "This document claims to be a PDF/A archive file, and PDF/A does not allow encryption. "
        + "Leave it unencrypted, or remove the PDF/A claim first.";

    /// <summary>Why a PDF/UA-1 document is not encrypted in a way that stops screen readers.</summary>
    internal const string PdfUa =
        "This document claims to be PDF/UA-1, an accessibility standard, and PDF/UA-1 needs an encrypted file to let screen readers read it (clause 7.16). "
        + "Allow screen readers in the permissions, or remove the PDF/UA claim first.";

    /// <summary>Why a document opened with a user password is not changed.</summary>
    internal const string NeedsOwner =
        "This document was opened with its user password, which limits what may be done with it. "
        + "Only the owner password can change or remove that protection.";

    /// <summary>Why a document that is not protected has no protection to remove.</summary>
    internal const string NotProtected = "This document is not protected, so there is no protection to remove.";
}
