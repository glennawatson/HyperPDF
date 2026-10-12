// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Reading;

/// <summary>A consecutive range of page characters.</summary>
/// <param name="Start">The first character index.</param>
/// <param name="Count">The number of characters.</param>
[DebuggerDisplay("ReadingCharacterRun: {Start}, {Count}")]
public readonly record struct ReadingCharacterRun(int Start, int Count);
