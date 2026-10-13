// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace HyperPdfLibrary.Security;

/// <summary>The algorithms of an ECDH recipient that the reader supports.</summary>
/// <param name="Hash">The X9.63 KDF hash.</param>
/// <param name="Cofactor">Whether the scheme uses cofactor ECDH.</param>
/// <param name="WrapLength">The AES key-encryption key length in bytes.</param>
/// <param name="ContentLength">The AES content-encryption key length in bytes.</param>
internal readonly record struct CmsKeyAgreementScheme(HashAlgorithmName Hash, bool Cofactor, int WrapLength, int ContentLength);
