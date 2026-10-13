// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>A link annotation as PDFium reads it.</summary>
/// <param name="Bounds">The link's <c>/Rect</c>.</param>
/// <param name="Action">The type of the link's action.</param>
/// <param name="PageIndex">The destination page, or -1 when the link has none.</param>
/// <param name="Uri">The URI action's address, or <see langword="null"/>.</param>
/// <param name="FilePath">The file a Launch, GoToR or GoToE action names, or <see langword="null"/>.</param>
internal sealed record PdfiumLink(PdfiumRect Bounds, PdfiumActionType Action, int PageIndex, string? Uri, string? FilePath);
