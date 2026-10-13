// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>An outline entry as PDFium reads it.</summary>
/// <param name="Title">The title.</param>
/// <param name="Count">The entry's <c>/Count</c>: positive when open, negative when closed.</param>
/// <param name="PageIndex">The page its destination or go-to action reaches, or -1.</param>
/// <param name="Uri">The address of its URI action, or <see langword="null"/>.</param>
/// <param name="Children">The entries below it.</param>
internal sealed record PdfiumBookmark(string Title, int Count, int PageIndex, string? Uri, IReadOnlyList<PdfiumBookmark> Children);
