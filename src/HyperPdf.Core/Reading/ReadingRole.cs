// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Reading;

/// <summary>What a block is and, for a heading, how deep.</summary>
/// <param name="Kind">What the block is.</param>
/// <param name="Level">The heading level, 1 to 6, for a heading; zero otherwise.</param>
[DebuggerDisplay("ReadingRole: {Kind} {Level}")]
internal readonly record struct ReadingRole(ReadingBlockKind Kind, int Level);
