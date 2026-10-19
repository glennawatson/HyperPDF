// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.XObjects;

/// <summary>
/// The external page a form XObject stands in for, from its <c>/Ref</c> dictionary (ISO 32000-2 §8.10.4, Table 98).
/// The library only describes the target. It never opens the file or the URL; the form's own content is the proxy that is drawn.
/// </summary>
/// <param name="FileName">The target file name or URL from <c>/F</c>, or an empty string when it cannot be read.</param>
/// <param name="IsUrl">Whether <c>/F</c> is a URL file specification.</param>
/// <param name="PageIndex">The zero-based page when <c>/Page</c> is an integer, or null.</param>
/// <param name="PageLabel">The page label when <c>/Page</c> is a text string, or null.</param>
/// <param name="PermanentId">The first <c>/ID</c> string (the permanent file identifier), or null.</param>
/// <param name="ChangingId">The second <c>/ID</c> string (the changing file identifier), or null.</param>
/// <param name="Proxy">The form XObject whose content is drawn in place of the external page.</param>
[DebuggerDisplay("PdfReferenceXObject: {FileName} page {PageIndex}{PageLabel}")]
public sealed record PdfReferenceXObject(
    string FileName,
    bool IsUrl,
    int? PageIndex,
    string? PageLabel,
    byte[]? PermanentId,
    byte[]? ChangingId,
    PdfStream Proxy);
