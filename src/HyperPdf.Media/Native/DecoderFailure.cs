// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Media.Native;

/// <summary>Why an operating system decoder could not be set up: the step that failed and the code the system gave.</summary>
/// <param name="Step">What was being done.</param>
/// <param name="Result">The system's result code, or 0 when it gave none.</param>
[DebuggerDisplay("DecoderFailure: {Step} 0x{Result,h}")]
internal readonly record struct DecoderFailure(string Step, int Result)
{
    /// <summary>Gets the failure as a sentence.</summary>
    /// <returns>The sentence.</returns>
    public override string ToString() => $"{Step} (0x{Result:X8})";
}
