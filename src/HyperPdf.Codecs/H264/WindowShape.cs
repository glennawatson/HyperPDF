// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>The widths of the reference window and the block it surrounds, which luma motion compensation indexes by.</summary>
/// <param name="Stride">The samples in a row of the window.</param>
/// <param name="BlockWidth">The samples in a row of the block.</param>
[DebuggerDisplay("WindowShape: {Stride} wide window, {BlockWidth} wide block")]
internal readonly record struct WindowShape(int Stride, int BlockWidth);
