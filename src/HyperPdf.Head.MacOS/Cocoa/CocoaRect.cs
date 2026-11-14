// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>CGRect, in the field order and layout of the C structure: four doubles. Cocoa measures from the bottom left corner.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The bottom edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CocoaRect(double X, double Y, double Width, double Height);
