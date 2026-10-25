// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>How the test HT encoder codes one quad.</summary>
/// <param name="Rho">The significance pattern.</param>
/// <param name="Context">The VLC context.</param>
/// <param name="Residual">The unsigned residual u: the bound less the context term.</param>
/// <param name="Bound">The exponent bound U.</param>
/// <param name="Code">The VLC codeword.</param>
internal readonly record struct JpxTestHtQuad(int Rho, int Context, int Residual, int Bound, JpxTestHtCode Code);
