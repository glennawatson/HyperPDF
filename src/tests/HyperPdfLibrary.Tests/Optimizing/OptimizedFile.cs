// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Optimizing;

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>The output of one optimisation run.</summary>
/// <param name="Bytes">The optimised file.</param>
/// <param name="Report">What the run did.</param>
[DebuggerDisplay("OptimizedFile: {Bytes.Length} bytes")]
internal sealed record OptimizedFile(byte[] Bytes, PdfOptimizeReport Report);
