// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>The validated first-object parameters of a linearized PDF.</summary>
/// <param name="FileLength">The /L file length in bytes.</param>
/// <param name="PageCount">The /N page count.</param>
/// <param name="FirstPageIndex">The /P page index shown first.</param>
/// <param name="FirstPageObjectNumber">The /O first-page object number.</param>
/// <param name="FirstPageEndOffset">The /E end of the first-page section.</param>
/// <param name="MainXrefOffset">The /T offset of the main cross-reference entries.</param>
/// <param name="Hints">The one or two /H hint-stream ranges, or empty when absent.</param>
[DebuggerDisplay("PdfLinearizationInfo: {PageCount} pages, {FileLength} bytes")]
public sealed record PdfLinearizationInfo(
    long FileLength,
    int PageCount,
    int FirstPageIndex,
    int FirstPageObjectNumber,
    long FirstPageEndOffset,
    long MainXrefOffset,
    PdfHintRange[] Hints);
