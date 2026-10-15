// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A run of rows of text pieces that may make a table.</summary>
/// <param name="First">The first row.</param>
/// <param name="Last">The last row, which has more than one piece.</param>
[DebuggerDisplay("RowRun: rows {First} to {Last}")]
internal readonly record struct RowRun(int First, int Last);
