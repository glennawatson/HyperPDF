// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Render.Skia;

namespace HyperPdf.Benchmarks;

/// <summary>Opens benchmark documents with an owned drawing service instance.</summary>
internal static class BenchmarkDrawing
{
    /// <summary>Creates services for one document.</summary>
    /// <returns>The services.</returns>
    internal static PdfDrawingServices CreateServices() => new(new PdfRenderBackendRegistration().UseSkia());

    /// <summary>Opens PDF bytes for drawing.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <param name="password">The password, or null.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(byte[] bytes, string? password) =>
        Open(bytes, password, CreateServices());

    /// <summary>Opens PDF bytes with the caller's reusable services.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <param name="password">The password, or null.</param>
    /// <param name="drawingServices">The drawing services reused by this benchmark.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(byte[] bytes, string? password, PdfDrawingServices drawingServices) =>
        PdfDocumentReader.OpenWith(bytes, OptionsFor(password, drawingServices));

    /// <summary>Opens a PDF path for drawing.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="password">The password, or null.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(string path, string? password) =>
        PdfDocumentReader.OpenWith(path, new PdfOpenOptions { Password = password, DrawingServices = CreateServices() });

    /// <summary>Opens a PDF stream for drawing.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="password">The password, or null.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(Stream stream, string? password) =>
        Open(stream, password, CreateServices());

    /// <summary>Opens a PDF stream with the caller's reusable services.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="password">The password, or null.</param>
    /// <param name="drawingServices">The drawing services reused by this benchmark.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(Stream stream, string? password, PdfDrawingServices drawingServices) =>
        PdfDocumentReader.OpenWith(stream, OptionsFor(password, drawingServices));

    /// <summary>Opens PDF bytes asynchronously for drawing.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <param name="password">The password, or null.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The document.</returns>
    internal static ValueTask<PdfDocument> OpenAsync(byte[] bytes, string? password, CancellationToken cancellationToken) =>
        OpenAsync(bytes, password, CreateServices(), cancellationToken);

    /// <summary>Opens PDF bytes asynchronously with the caller's reusable services.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <param name="password">The password, or null.</param>
    /// <param name="drawingServices">The drawing services reused by this benchmark.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The document.</returns>
    internal static ValueTask<PdfDocument> OpenAsync(byte[] bytes, string? password, PdfDrawingServices drawingServices, CancellationToken cancellationToken) =>
        PdfDocumentReader.OpenWithAsync(new MemoryPdfByteSource(bytes), true, OptionsFor(password, drawingServices), cancellationToken);

    /// <summary>Opens a PDF stream asynchronously for drawing.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="password">The password, or null.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The document.</returns>
    internal static ValueTask<PdfDocument> OpenAsync(Stream stream, string? password, CancellationToken cancellationToken) =>
        OpenAsync(stream, password, CreateServices(), cancellationToken);

    /// <summary>Opens a PDF stream asynchronously with the caller's reusable services.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="password">The password, or null.</param>
    /// <param name="drawingServices">The drawing services reused by this benchmark.</param>
    /// <param name="cancellationToken">Cancels the open.</param>
    /// <returns>The document.</returns>
    internal static ValueTask<PdfDocument> OpenAsync(Stream stream, string? password, PdfDrawingServices drawingServices, CancellationToken cancellationToken) =>
        PdfDocumentReader.OpenWithAsync(stream, OptionsFor(password, drawingServices), cancellationToken);

    /// <summary>Gets the open options for a service instance, made once per instance when there is no password.</summary>
    /// <param name="password">The password, or null.</param>
    /// <param name="drawingServices">The drawing services.</param>
    /// <returns>The options.</returns>
    private static PdfOpenOptions OptionsFor(string? password, PdfDrawingServices drawingServices) =>
        password is null
            ? drawingServices.PasswordlessOpenOptions
            : new() { Password = password, DrawingServices = drawingServices };
}
