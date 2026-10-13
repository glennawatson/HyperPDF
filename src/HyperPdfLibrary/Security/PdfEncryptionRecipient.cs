// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Security;

/// <summary>A person who may open a public-key encrypted file, and what they may do with it.</summary>
/// <param name="Certificate">The recipient's certificate. Only the public key is used, so the private key is not needed.</param>
/// <param name="Permissions">What the recipient may do. Recipients with the same permissions share one recipient list.</param>
[DebuggerDisplay("PdfEncryptionRecipient: {Certificate.Subject}")]
public sealed record PdfEncryptionRecipient(X509Certificate2 Certificate, PdfAccessPermissions Permissions);
