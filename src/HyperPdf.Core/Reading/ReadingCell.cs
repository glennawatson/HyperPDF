// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Reading;

/// <summary>Where a block came from: the document's structure tags or its layout, its table position and its language.</summary>
/// <param name="IsTagged">Whether the block comes from the document's structure tags.</param>
/// <param name="Row">The table row of a table cell, from 1; zero otherwise.</param>
/// <param name="Column">The table column of a table cell, from 1; zero otherwise.</param>
/// <param name="Language">The language the tags ask for when it differs from the surrounding text, or <see langword="null"/>.</param>
[DebuggerDisplay("ReadingCell: tagged {IsTagged}, row {Row}, column {Column}")]
internal readonly record struct ReadingCell(bool IsTagged, int Row, int Column, string? Language);
