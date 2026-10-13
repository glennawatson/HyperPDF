// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Pdf20;

/// <summary>The outcome of a conversion and the converted file.</summary>
/// <param name="Result">The outcome.</param>
/// <param name="File">The file, or <see langword="null"/> when the conversion stopped.</param>
[DebuggerDisplay("Pdf20ConversionOutput: {Result}")]
internal sealed record Pdf20ConversionOutput(Pdf20ConversionResult Result, byte[]? File);
