// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>The code a font shows one character with.</summary>
/// <param name="Value">The code, high byte first.</param>
/// <param name="Length">The bytes the code takes: one or two.</param>
[DebuggerDisplay("FormCode: {Value} in {Length} bytes")]
internal readonly record struct FormCode(int Value, int Length);
