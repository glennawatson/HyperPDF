// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Annotations;

namespace HyperPdf.Engine;

/// <summary>A built in font and its size.</summary>
/// <param name="Font">The font.</param>
/// <param name="Size">The size in points.</param>
[DebuggerDisplay("TextLook: {Font} {Size}pt")]
internal readonly record struct TextLook(AppearanceFont Font, float Size);
