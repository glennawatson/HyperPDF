// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Head.MacOS.Cocoa;

/// <summary>CGPoint and CGSize, in the field order and layout of the C structures: two doubles.</summary>
/// <param name="X">The x, or the width.</param>
/// <param name="Y">The y, or the height.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CocoaPoint(double X, double Y);
