// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.ViewModels.Signing;

/// <summary>The public certificates of an external signer, read from the files the user chose.</summary>
/// <param name="Identity">The certificates, signing certificate first, and the kind of key.</param>
/// <param name="Subject">The name on the signing certificate.</param>
[DebuggerDisplay("LoadedSigner: {Subject}")]
public sealed record LoadedSigner(SignerIdentity Identity, string Subject);
