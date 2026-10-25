// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>One terminated codeword segment.</summary>
/// <param name="Passes">The coding passes it holds.</param>
/// <param name="Data">The coded bytes.</param>
[DebuggerDisplay("JpxTestSegment: {Passes} passes, {Data.Length} bytes")]
internal sealed record JpxTestSegment(int Passes, byte[] Data);
