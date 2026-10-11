// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Render.Skia;

namespace HyperPdfLibrary.Tests;

/// <summary>Creates Skia drawing services for direct core tests.</summary>
internal static class SkiaTestRegistration
{
    /// <summary>Creates drawing services for one test document.</summary>
    /// <returns>The services.</returns>
    internal static PdfDrawingServices CreateServices() => new(new PdfRenderBackendRegistration().UseSkia());

    /// <summary>Opens bytes with their own Skia services.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <param name="password">The password, or null.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(byte[] bytes, string? password) =>
        PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { Password = password, DrawingServices = CreateServices() });

    /// <summary>Opens a path with its own Skia services.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="password">The password, or null.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(string path, string? password) =>
        PdfDocumentReader.OpenWith(path, new PdfOpenOptions { Password = password, DrawingServices = CreateServices() });

    /// <summary>Opens a stream with its own Skia services.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="password">The password, or null.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(Stream stream, string? password) =>
        PdfDocumentReader.OpenWith(stream, new PdfOpenOptions { Password = password, DrawingServices = CreateServices() });

    /// <summary>Opens bytes asynchronously with their own Skia services.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <param name="password">The password, or null.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The document.</returns>
    internal static ValueTask<PdfDocument> OpenAsync(byte[] bytes, string? password, CancellationToken cancellationToken) =>
        PdfDocumentReader.OpenWithAsync(new MemoryPdfByteSource(bytes), true, new PdfOpenOptions { Password = password, DrawingServices = CreateServices() }, cancellationToken);

    /// <summary>Opens a stream asynchronously with its own Skia services.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="password">The password, or null.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The document.</returns>
    internal static ValueTask<PdfDocument> OpenAsync(Stream stream, string? password, CancellationToken cancellationToken) =>
        PdfDocumentReader.OpenWithAsync(stream, new PdfOpenOptions { Password = password, DrawingServices = CreateServices() }, cancellationToken);
}
