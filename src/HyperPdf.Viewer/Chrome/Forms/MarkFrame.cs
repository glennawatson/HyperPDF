// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>The picture of a signature the signature window shows, and the mark it was made from.</summary>
/// <param name="Surface">The picture, or <see langword="null"/> before the first one.</param>
/// <param name="Mark">The mark the picture shows.</param>
[DebuggerDisplay("MarkFrame: {Surface?.Width} x {Surface?.Height}")]
internal sealed record MarkFrame(IRenderSurface? Surface, SignatureMark? Mark)
{
    /// <summary>Gets the frame before any picture.</summary>
    internal static MarkFrame Empty { get; } = new(null, null);
}
