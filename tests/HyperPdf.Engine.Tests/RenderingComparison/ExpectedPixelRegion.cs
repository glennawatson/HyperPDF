// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>An independently derived interior rectangle, away from rasterized edges.</summary>
/// <param name="X">The left pixel coordinate.</param>
/// <param name="Y">The top pixel coordinate.</param>
/// <param name="Width">The rectangle width.</param>
/// <param name="Height">The rectangle height.</param>
/// <param name="Expected">The expected BGRA32 color.</param>
/// <param name="Tolerance">The largest allowed error in any channel.</param>
/// <param name="Clause">The standard clause supporting this expectation.</param>
internal readonly record struct ExpectedPixelRegion(int X, int Y, int Width, int Height, Bgra32Color Expected, byte Tolerance, string Clause);
