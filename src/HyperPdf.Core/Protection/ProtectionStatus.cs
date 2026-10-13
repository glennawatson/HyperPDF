// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Protection;

/// <summary>How the open document is protected.</summary>
/// <param name="IsProtected">Whether the document is encrypted.</param>
/// <param name="CanChange">Whether the protection can be changed without more passwords: the document is not protected, was opened as its owner, or allows everything.</param>
/// <param name="UsesCertificates">Whether it is encrypted for certificates, which have no owner password.</param>
[DebuggerDisplay("ProtectionStatus: protected {IsProtected}, can change {CanChange}")]
public sealed record ProtectionStatus(bool IsProtected, bool CanChange, bool UsesCertificates);
