// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>The fields a bitmap's info header holds, as read, before they are checked.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="RawHeight">The height in pixels; negative when the rows are stored from the top.</param>
/// <param name="Bits">The bits in a pixel.</param>
/// <param name="Compression">The compression value.</param>
/// <param name="Colors">The colours the palette holds; zero means all that the pixel size allows.</param>
/// <param name="HeaderSize">The size of the info header.</param>
[DebuggerDisplay("BmpFields: {Width}x{RawHeight}, {Bits} bpp, compression {Compression}")]
internal readonly record struct BmpFields(int Width, int RawHeight, int Bits, int Compression, int Colors, int HeaderSize);
