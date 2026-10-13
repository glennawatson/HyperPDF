// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Security;

/// <summary>What a qpdf run printed and wrote.</summary>
/// <param name="Output">What qpdf wrote to standard output and standard error.</param>
/// <param name="OutputFile">The file qpdf wrote to the output token's path, or <see langword="null"/> when it wrote none.</param>
internal readonly record struct QpdfCommandResult(string Output, byte[]? OutputFile);
