// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>One repair made while reading a damaged document.</summary>
/// <param name="Description">What was wrong and what was done, in plain words.</param>
/// <param name="ObjectNumber">The PDF object involved, or 0 when none.</param>
/// <param name="Offset">The byte offset in the file, or -1 when none.</param>
[DebuggerDisplay("RepairNote: {Description} object {ObjectNumber}")]
public sealed record RepairNote(string Description, int ObjectNumber, long Offset);
