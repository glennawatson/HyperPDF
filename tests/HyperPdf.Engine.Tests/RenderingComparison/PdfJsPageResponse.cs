// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text.Json;

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Owns the browser's raw pixel response and page metadata.</summary>
/// <param name="Pixels">The browser's straight RGBA bytes.</param>
/// <param name="Metadata">The page's render metadata.</param>
internal sealed record PdfJsPageResponse(byte[] Pixels, JsonElement Metadata);
