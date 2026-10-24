// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>One code read from a string.</summary>
/// <param name="Code">The character code.</param>
/// <param name="Length">The bytes it used.</param>
[DebuggerDisplay("CodeRead: {Code} ({Length})")]
internal readonly record struct CodeRead(int Code, int Length);
