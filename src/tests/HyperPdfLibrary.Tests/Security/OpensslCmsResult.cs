// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Security;

/// <summary>What <c>openssl cms -decrypt</c> printed and produced.</summary>
/// <param name="Output">What openssl wrote to standard output and standard error.</param>
/// <param name="Content">The decrypted content, or <see langword="null"/> when openssl wrote none.</param>
internal readonly record struct OpensslCmsResult(string Output, byte[]? Content);
