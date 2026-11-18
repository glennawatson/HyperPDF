// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Icons;

/// <summary>What makes one drawn icon different from another.</summary>
/// <param name="Icon">The icon.</param>
/// <param name="Size">The side of the icon in device pixels.</param>
/// <param name="Color">The colour as ARGB.</param>
[DebuggerDisplay("IconKey: {Icon} {Size} px")]
internal readonly record struct IconKey(ChromeIcon Icon, int Size, uint Color);
