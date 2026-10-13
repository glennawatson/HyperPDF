// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>The colour of a rendered pixel.</summary>
/// <param name="Red">The red level.</param>
/// <param name="Green">The green level.</param>
/// <param name="Blue">The blue level.</param>
[DebuggerDisplay("PixelColor: ({Red}, {Green}, {Blue})")]
internal readonly record struct PixelColor(byte Red, byte Green, byte Blue);
