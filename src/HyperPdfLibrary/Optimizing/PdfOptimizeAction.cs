// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>One change an optimisation run made.</summary>
/// <param name="Category">The kind of change.</param>
/// <param name="ObjectNumber">The source object it applies to, or zero for the whole document.</param>
/// <param name="Description">What was done, in plain words.</param>
/// <param name="BytesBefore">The object's stored size before, or zero when not measured.</param>
/// <param name="BytesAfter">The object's stored size after, or zero when not measured.</param>
[DebuggerDisplay("PdfOptimizeAction: {Category} {ObjectNumber} {Description}")]
public readonly record struct PdfOptimizeAction(PdfOptimizeCategory Category, int ObjectNumber, string Description, long BytesBefore, long BytesAfter);
