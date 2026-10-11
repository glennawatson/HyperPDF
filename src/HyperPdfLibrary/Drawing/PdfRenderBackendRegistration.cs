// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Drawing;

/// <summary>The drawing services selected before documents are opened.</summary>
[DebuggerDisplay("Drawing {Backend}, fonts {Fonts}, images {Images}")]
public sealed record PdfRenderBackendRegistration
{
    /// <summary>Gets the optional drawing backend.</summary>
    public IPdfRenderBackend? Backend { get; init; }

    /// <summary>Gets the optional native font provider.</summary>
    public IPdfFontProvider? Fonts { get; init; }

    /// <summary>Gets the optional image codec.</summary>
    public IPdfImageCodec? Images { get; init; }

    /// <summary>Gets the optional decoder for unsupported image formats.</summary>
    public Func<PdfStream, CancellationToken, IPdfRenderImage?>? UnsupportedImageDecoder { get; init; }
}
