// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Graphics;

/// <summary>A CIE L*a*b* colour.</summary>
/// <param name="L">The lightness.</param>
/// <param name="A">The green to red axis.</param>
/// <param name="B">The blue to yellow axis.</param>
internal readonly record struct CmykLab(double L, double A, double B);
