// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>Whether the integrity protection of an encrypted document (its PDF MAC) was checked and holds.</summary>
public enum DocumentIntegrityStatus
{
    /// <summary>The document has no integrity protection to check.</summary>
    NotProtected = 0,

    /// <summary>The protection was checked and the document is unchanged.</summary>
    Intact = 1,

    /// <summary>The protection does not match: the document was changed or damaged, or the protection is not valid.</summary>
    Invalid = 2,

    /// <summary>The document requires integrity protection, but a revision has none.</summary>
    Missing = 3,

    /// <summary>The protection uses a method this computer cannot check, so the document is not known to be intact.</summary>
    Unverified = 4,
}
