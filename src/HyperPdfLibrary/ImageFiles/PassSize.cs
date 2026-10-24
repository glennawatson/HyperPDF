// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>The size of one interlace pass, in pixels.</summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("PassSize: {Width}x{Height}")]
internal readonly record struct PassSize(int Width, int Height);
