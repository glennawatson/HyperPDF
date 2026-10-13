// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signing;

/// <summary>The signed attributes of a signature and what they were made from.</summary>
/// <param name="Spec">What the container is made of.</param>
/// <param name="DocumentDigest">The digest of the file's signed bytes.</param>
/// <param name="SignedAttributes">The DER signed attributes.</param>
[DebuggerDisplay("PdfSignedAttributes: {SignedAttributes.Length} bytes")]
internal readonly record struct PdfSignedAttributes(PdfCmsSpec Spec, byte[] DocumentDigest, byte[] SignedAttributes);
