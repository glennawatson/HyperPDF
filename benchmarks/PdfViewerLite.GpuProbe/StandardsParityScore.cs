// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.GpuProbe;

/// <summary>The separate sources of pixel difference for one fixture.</summary>
/// <param name="ExactPixels">Pixels identical on CPU and GPU.</param>
/// <param name="QuantizationPixels">Pixels differing by at most two channel values.</param>
/// <param name="EdgePixels">Larger differences next to a sharp CPU edge.</param>
/// <param name="MaterialPixels">Larger differences away from sharp edges.</param>
/// <param name="MaxChannelDelta">The greatest channel delta.</param>
/// <param name="OracleFailures">Pixels failing a standards-derived interior expectation.</param>
/// <param name="ReadbackBytes">Bytes read from the GPU for this test case.</param>
internal readonly record struct StandardsParityScore(int ExactPixels, int QuantizationPixels, int EdgePixels, int MaterialPixels, int MaxChannelDelta, int OracleFailures, int ReadbackBytes);
