// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A run of characters on a page.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Start">The first character index.</param>
/// <param name="Length">The number of characters.</param>
[DebuggerDisplay("TextMatch: Page {PageIndex} [{Start}+{Length}]")]
public readonly record struct TextMatch(int PageIndex, int Start, int Length);
