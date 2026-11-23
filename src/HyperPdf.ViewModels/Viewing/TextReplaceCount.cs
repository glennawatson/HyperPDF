// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>What a find and replace changed.</summary>
/// <param name="Replaced">How many times the words were replaced.</param>
/// <param name="Pages">The pages that changed, lowest first.</param>
[DebuggerDisplay("TextReplaceCount: {Replaced} on {Pages.Count} pages")]
internal sealed record TextReplaceCount(int Replaced, List<int> Pages);
