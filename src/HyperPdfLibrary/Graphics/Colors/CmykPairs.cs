// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>The products of two inks for a block of pixels, which the products of three inks extend.</summary>
/// <typeparam name="TFloat">The float lanes.</typeparam>
/// <param name="CC">Cyan times cyan.</param>
/// <param name="CM">Cyan times magenta.</param>
/// <param name="CY">Cyan times yellow.</param>
/// <param name="CK">Cyan times black.</param>
/// <param name="MM">Magenta times magenta.</param>
/// <param name="MY">Magenta times yellow.</param>
/// <param name="MK">Magenta times black.</param>
/// <param name="YY">Yellow times yellow.</param>
/// <param name="YK">Yellow times black.</param>
/// <param name="KK">Black times black.</param>
internal readonly record struct CmykPairs<TFloat>(TFloat CC, TFloat CM, TFloat CY, TFloat CK, TFloat MM, TFloat MY, TFloat MK, TFloat YY, TFloat YK, TFloat KK)
    where TFloat : unmanaged;
