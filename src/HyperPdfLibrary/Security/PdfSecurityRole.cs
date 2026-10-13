// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>Who opened an encrypted document.</summary>
public enum PdfSecurityRole
{
    /// <summary>A user, bound by the document's permissions. They cannot change or remove the protection.</summary>
    User = 0,

    /// <summary>
    /// The owner: the owner password was used (or the user and owner password are the same), or a public-key recipient has
    /// the right to change the encryption. The owner may change the permissions and passwords, or remove the protection.
    /// </summary>
    Owner = 1,
}
