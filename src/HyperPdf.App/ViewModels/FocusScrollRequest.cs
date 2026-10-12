// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.App.ViewModels;

/// <summary>Asks the Focus Mode view to bring a place into view.</summary>
/// <param name="PageIndex">The page.</param>
/// <param name="Fraction">How far down the page, from 0 (top) to 1 (bottom); the block nearest it is shown at the top.</param>
[DebuggerDisplay("FocusScrollRequest: Page {PageIndex} at {Fraction}")]
public readonly record struct FocusScrollRequest(int PageIndex, double Fraction);
