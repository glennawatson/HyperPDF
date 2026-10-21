// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>Where a separation page sits: its printed page and its position among that page's separations.</summary>
/// <param name="Group">The printed page.</param>
/// <param name="Position">The page's position in <see cref="SeparationGroup.Pages"/>.</param>
[DebuggerDisplay("PageSeparationSlot: printed page {Group.PrintedPage} position {Position}")]
internal readonly record struct PageSeparationSlot(SeparationGroup Group, int Position);
