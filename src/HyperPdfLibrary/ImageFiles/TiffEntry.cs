// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>One tag of a TIFF directory.</summary>
/// <param name="Type">The field type: 1 byte, 3 short, 4 long and others.</param>
/// <param name="Count">The number of values.</param>
/// <param name="ValueAt">The offset in the file of the entry's value field, which holds the values or the offset of the values.</param>
[DebuggerDisplay("TiffEntry: type {Type} x {Count}")]
internal readonly record struct TiffEntry(int Type, int Count, int ValueAt);
