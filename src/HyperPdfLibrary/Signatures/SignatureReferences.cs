// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signatures;

/// <summary>The transforms read from a signature's /Reference array.</summary>
/// <param name="DocMdp">The DocMDP permission, or None.</param>
/// <param name="FieldMdp">The FieldMDP lock, or <see langword="null"/>.</param>
/// <param name="UsageRights">The UR3 usage rights, or <see langword="null"/>.</param>
/// <param name="DigestMethods">Each reference's /DigestMethod, in order.</param>
[DebuggerDisplay("SignatureReferences: {DocMdp}")]
internal readonly record struct SignatureReferences(PdfMdpPermission DocMdp, PdfFieldLock? FieldMdp, PdfUsageRights? UsageRights, PdfDigestMethod[] DigestMethods);
