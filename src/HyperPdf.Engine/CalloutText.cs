// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Engine;

/// <summary>How a callout's text looks.</summary>
/// <param name="Size">The text size in points.</param>
/// <param name="Color">The colour as 0xRRGGBB.</param>
[DebuggerDisplay("CalloutText: {Size}pt {Color}")]
internal readonly record struct CalloutText(float Size, uint Color);
