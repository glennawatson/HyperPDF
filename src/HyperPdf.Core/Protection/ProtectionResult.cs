// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Protection;

/// <summary>The outcome of protecting a copy or removing its protection.</summary>
/// <param name="Refusal">Why no file was written, in plain words; <see langword="null"/> when it was written.</param>
/// <param name="Notes">Things to know about the file that was written.</param>
[DebuggerDisplay("ProtectionResult: written {Written}")]
public sealed record ProtectionResult(string? Refusal, IReadOnlyList<string> Notes)
{
    /// <summary>Gets a value indicating whether the file was written.</summary>
    public bool Written => Refusal is null;
}
