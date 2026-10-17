// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Metadata;

/// <summary>The text nodes of one property or array item.</summary>
/// <param name="Single">The first node, shared until another node arrives.</param>
/// <param name="Buffer">The leased storage for multiple nodes.</param>
/// <param name="Length">The used characters in the leased storage.</param>
internal readonly record struct XmpTextValue(string? Single, char[]? Buffer, int Length);
