// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;

namespace HyperPdfLibrary.Security;

/// <summary>The inputs of the X9.63 key derivation for one ECDH recipient.</summary>
/// <param name="PrivateKey">The recipient's private key, held by its provider.</param>
/// <param name="PeerKey">The originator's ephemeral public key.</param>
/// <param name="Hash">The KDF hash.</param>
/// <param name="SharedInfo">Four bytes for the KDF counter, then the ECC-CMS-SharedInfo DER.</param>
internal readonly record struct CmsKeyAgreementInputs(ECDiffieHellman PrivateKey, ECDiffieHellmanPublicKey PeerKey, HashAlgorithmName Hash, byte[] SharedInfo);
