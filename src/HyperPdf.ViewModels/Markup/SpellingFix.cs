// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Reading;

namespace HyperPdf.ViewModels.Markup;

/// <summary>A correction for a misspelled word in the field being edited.</summary>
/// <param name="Word">The word's place in the text.</param>
/// <param name="Replacement">The correction.</param>
[DebuggerDisplay("SpellingFix: {Replacement}")]
public sealed record SpellingFix(TextRange Word, string Replacement);
