// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Security;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>What the library reports about the protection of a file it opened.</summary>
/// <param name="Revision">The security handler revision.</param>
/// <param name="Permissions">The permission word.</param>
/// <param name="Role">Who the password made the reader.</param>
/// <param name="EncryptMetadata">Whether metadata is encrypted.</param>
internal readonly record struct OpenedProtection(int Revision, int Permissions, PdfSecurityRole Role, bool EncryptMetadata);
