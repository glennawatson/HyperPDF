// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>A generated well-formed PDF and selected standards checks, without any PDF/A claim.</summary>
/// <param name="Pdf">The generated PDF bytes.</param>
/// <param name="Oracle">The independently derived interior expectations.</param>
internal sealed record StandardsRenderFixture(byte[] Pdf, StandardsOracle Oracle);
