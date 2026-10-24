// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>The column and row of a sample inside a block.</summary>
/// <param name="Column">The column.</param>
/// <param name="Row">The row.</param>
[DebuggerDisplay("SamplePlace: column {Column}, row {Row}")]
internal readonly record struct SamplePlace(int Column, int Row);
