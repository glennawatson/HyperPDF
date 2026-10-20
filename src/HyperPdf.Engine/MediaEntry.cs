// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Objects;

namespace HyperPdf.Engine;

/// <summary>A piece of media found in the document, with the stream that holds it.</summary>
/// <param name="Info">What the viewer shows about it.</param>
/// <param name="Data">The stream holding the data.</param>
/// <param name="Sound">The sound whose samples the stream holds, when it is a sound annotation that can be written as a WAVE file.</param>
[DebuggerDisplay("MediaEntry: {Info}")]
internal sealed record MediaEntry(DocumentMedia Info, PdfStream Data, PdfSound? Sound);
