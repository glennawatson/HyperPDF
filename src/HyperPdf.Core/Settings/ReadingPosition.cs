// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Settings;

/// <summary>Where reading aloud last stopped in a document.</summary>
/// <param name="Page">The page.</param>
/// <param name="Character">The page character the sentence starts at.</param>
[DebuggerDisplay("ReadingPosition: Page {Page}, character {Character}")]
public sealed record ReadingPosition(int Page, int Character);
