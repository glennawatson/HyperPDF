// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Writing;

/// <summary>The result of <c>qpdf --check</c> on a file.</summary>
/// <param name="IsClean">Whether qpdf found no errors and no warnings.</param>
/// <param name="Output">What qpdf printed.</param>
internal readonly record struct QpdfOutcome(bool IsClean, string Output);
