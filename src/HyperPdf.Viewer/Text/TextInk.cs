// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Text;

/// <summary>What is written into the surface of a piece of text.</summary>
/// <param name="Coverage">The coverage of the text.</param>
/// <param name="Color">The colour as ARGB.</param>
[DebuggerDisplay("TextInk: colour {Color}")]
internal readonly record struct TextInk(CoverageBuffer Coverage, uint Color);
