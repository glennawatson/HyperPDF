// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>The running red, green and blue sums of the CMYK polynomial for a block of pixels.</summary>
/// <typeparam name="TFloat">The float lanes.</typeparam>
/// <param name="Red">The red lanes.</param>
/// <param name="Green">The green lanes.</param>
/// <param name="Blue">The blue lanes.</param>
internal readonly record struct CmykChannels<TFloat>(TFloat Red, TFloat Green, TFloat Blue)
    where TFloat : unmanaged;
