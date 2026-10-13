// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.PdfiumReference;

/// <summary>An <c>/AcroForm /XFA</c> packet as PDFium reads it.</summary>
/// <param name="Name">The packet name's string bytes, one character per byte; empty for a single XFA stream.</param>
/// <param name="Content">The decoded packet bytes.</param>
internal sealed record PdfiumXfaPacket(string Name, byte[] Content);
