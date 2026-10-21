// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Reading;

/// <summary>A character on a page, such as the start of a text selection.</summary>
/// <param name="Page">The zero based page.</param>
/// <param name="Char">The zero based character index on the page.</param>
[DebuggerDisplay("CharacterPosition: Page {Page}, character {Char}")]
public readonly record struct CharacterPosition(int Page, int Char);
