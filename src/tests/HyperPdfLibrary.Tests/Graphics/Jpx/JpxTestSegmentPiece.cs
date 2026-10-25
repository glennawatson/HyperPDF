// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>The part of a codeword segment one layer sends: some passes, and the segment's bytes when it completes it.</summary>
/// <param name="Passes">The passes.</param>
/// <param name="Data">The segment's bytes, or empty when a later layer completes the segment.</param>
[DebuggerDisplay("JpxTestSegmentPiece: {Passes} passes, {Data.Length} bytes")]
internal sealed record JpxTestSegmentPiece(int Passes, byte[] Data)
{
    /// <summary>Gets the bytes sent.</summary>
    internal int Length => Data.Length;
}
