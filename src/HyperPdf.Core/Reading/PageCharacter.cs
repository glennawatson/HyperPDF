// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Reading;

/// <summary>A character on a page, in viewer page space, as the engine reports it.</summary>
/// <param name="Value">The character.</param>
/// <param name="Bounds">Its box; empty for characters the engine inserted.</param>
/// <param name="FontSize">Its font size in points.</param>
/// <param name="Bold">Whether its font is bold.</param>
/// <param name="Generated">Whether the engine inserted it, as it does for spaces and line breaks it infers.</param>
[DebuggerDisplay("PageCharacter: {Value} {Bounds}")]
public readonly record struct PageCharacter(char Value, PageRect Bounds, float FontSize, bool Bold, bool Generated);
