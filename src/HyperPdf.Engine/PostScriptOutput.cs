// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Export;

namespace HyperPdf.Engine;

/// <summary>A PostScript file built in memory and what its export left out or drew differently.</summary>
/// <param name="Bytes">The file.</param>
/// <param name="Report">What the export left out or drew differently.</param>
[DebuggerDisplay("PostScriptOutput: {Bytes.Length} bytes")]
internal sealed record PostScriptOutput(byte[] Bytes, PdfPostScriptReport Report);
