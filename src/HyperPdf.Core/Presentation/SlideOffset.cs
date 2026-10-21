// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Presentation;

/// <summary>How far a sliding page is from its place.</summary>
/// <param name="X">The horizontal distance.</param>
/// <param name="Y">The vertical distance.</param>
[DebuggerDisplay("SlideOffset: ({X}, {Y})")]
public readonly record struct SlideOffset(double X, double Y);
