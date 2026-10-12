// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.ViewModels;

/// <summary>Asks the page canvas to scroll.</summary>
/// <param name="PageIndex">The zero based destination page.</param>
/// <param name="Target">An area of the page to bring into view, or <see langword="null"/> for the top of the page.</param>
/// <param name="OffsetFraction">The vertical position within the page from 0 to 1, used when <paramref name="Target"/> is null.</param>
[DebuggerDisplay("NavigationRequest: Page {PageIndex}")]
public sealed record NavigationRequest(int PageIndex, PageRect? Target, double OffsetFraction);
