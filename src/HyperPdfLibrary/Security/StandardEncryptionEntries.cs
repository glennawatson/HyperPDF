// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Security;

/// <summary>The values of a new standard security handler dictionary, and the file key they protect.</summary>
/// <param name="Revision">The revision: 4, 6 or 7.</param>
/// <param name="Permissions">The <c>/P</c> value.</param>
/// <param name="FileKey">The file encryption key.</param>
/// <param name="Owner">The <c>/O</c> value.</param>
/// <param name="User">The <c>/U</c> value.</param>
/// <param name="OwnerKey">The <c>/OE</c> value, or <see langword="null"/> before revision 6.</param>
/// <param name="UserKey">The <c>/UE</c> value, or <see langword="null"/> before revision 6.</param>
/// <param name="Perms">The <c>/Perms</c> value, or <see langword="null"/> before revision 6.</param>
[DebuggerDisplay("StandardEncryptionEntries: R{Revision}")]
internal sealed record StandardEncryptionEntries(
    int Revision,
    int Permissions,
    byte[] FileKey,
    byte[] Owner,
    byte[] User,
    byte[]? OwnerKey,
    byte[]? UserKey,
    byte[]? Perms);
