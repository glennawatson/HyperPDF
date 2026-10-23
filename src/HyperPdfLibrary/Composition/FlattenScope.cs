// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Where blocks are being flattened: how far in they are indented, which structure node they go under and which style paragraphs without one take.</summary>
/// <param name="Indent">The indent from the left edge of the text area, in points.</param>
/// <param name="Parent">The structure node the blocks' nodes go under, or <see langword="null"/> for the document.</param>
/// <param name="DefaultStyle">The style of paragraphs that name none, or <see langword="null"/> for Normal.</param>
[DebuggerDisplay("FlattenScope: indent {Indent}")]
internal readonly record struct FlattenScope(float Indent, StructNode? Parent, string? DefaultStyle);
