// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace HyperPdf.Ocr.Native;

/// <summary>
/// Source generated entry points of Tesseract's C API (<c>tesseract/capi.h</c>). <see cref="TesseractLibraryResolver"/>
/// binds <see cref="Library"/> by absolute path to the copy shipped with the app, as Skia is bound.
/// </summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>The native library name: <c>libtesseract.so</c>, <c>libtesseract.dylib</c> or <c>tesseract.dll</c>.</summary>
    internal const string Library = "tesseract";

    /// <summary>Native <c>TessBaseAPICreate</c>.</summary>
    /// <returns>The engine.</returns>
    [LibraryImport(Library)]
    internal static partial TesseractHandle TessBaseAPICreate();

    /// <summary>Native <c>TessBaseAPIDelete</c>.</summary>
    /// <param name="handle">The engine.</param>
    [LibraryImport(Library)]
    internal static partial void TessBaseAPIDelete(nint handle);

    /// <summary>Native <c>TessBaseAPIInit3</c>.</summary>
    /// <param name="handle">The engine.</param>
    /// <param name="dataPath">The tessdata directory.</param>
    /// <param name="language">The language, for example <c>eng</c>.</param>
    /// <returns>0 on success.</returns>
    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int TessBaseAPIInit3(TesseractHandle handle, string dataPath, string language);

    /// <summary>Native <c>TessBaseAPISetPageSegMode</c>.</summary>
    /// <param name="handle">The engine.</param>
    /// <param name="mode">The page segmentation mode.</param>
    [LibraryImport(Library)]
    internal static partial void TessBaseAPISetPageSegMode(TesseractHandle handle, int mode);

    /// <summary>Native <c>TessBaseAPISetImage</c>.</summary>
    /// <param name="handle">The engine.</param>
    /// <param name="image">The pixels.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="bytesPerPixel">The bytes per pixel.</param>
    /// <param name="bytesPerLine">The bytes per line.</param>
    [LibraryImport(Library)]
    internal static partial void TessBaseAPISetImage(TesseractHandle handle, byte* image, int width, int height, int bytesPerPixel, int bytesPerLine);

    /// <summary>Native <c>TessBaseAPISetSourceResolution</c>.</summary>
    /// <param name="handle">The engine.</param>
    /// <param name="ppi">The resolution in pixels per inch.</param>
    [LibraryImport(Library)]
    internal static partial void TessBaseAPISetSourceResolution(TesseractHandle handle, int ppi);

    /// <summary>Native <c>TessBaseAPIRecognize</c>.</summary>
    /// <param name="handle">The engine.</param>
    /// <param name="monitor">An optional progress monitor.</param>
    /// <returns>0 on success.</returns>
    [LibraryImport(Library)]
    internal static partial int TessBaseAPIRecognize(TesseractHandle handle, nint monitor);

    /// <summary>Native <c>TessBaseAPIGetIterator</c>.</summary>
    /// <param name="handle">The engine.</param>
    /// <returns>The result iterator, or 0.</returns>
    [LibraryImport(Library)]
    internal static partial nint TessBaseAPIGetIterator(TesseractHandle handle);

    /// <summary>Native <c>TessBaseAPIClear</c>.</summary>
    /// <param name="handle">The engine.</param>
    [LibraryImport(Library)]
    internal static partial void TessBaseAPIClear(TesseractHandle handle);

    /// <summary>Native <c>TessBaseAPIEnd</c>.</summary>
    /// <param name="handle">The engine.</param>
    [LibraryImport(Library)]
    internal static partial void TessBaseAPIEnd(nint handle);

    /// <summary>Native <c>TessResultIteratorDelete</c>.</summary>
    /// <param name="iterator">The iterator.</param>
    [LibraryImport(Library)]
    internal static partial void TessResultIteratorDelete(nint iterator);

    /// <summary>Native <c>TessResultIteratorNext</c>.</summary>
    /// <param name="iterator">The iterator.</param>
    /// <param name="level">The iterator level.</param>
    /// <returns>Non-zero while there are more items.</returns>
    [LibraryImport(Library)]
    internal static partial int TessResultIteratorNext(nint iterator, int level);

    /// <summary>Native <c>TessResultIteratorGetUTF8Text</c>.</summary>
    /// <param name="iterator">The iterator.</param>
    /// <param name="level">The iterator level.</param>
    /// <returns>The UTF-8 text, freed with <see cref="TessDeleteText"/>.</returns>
    [LibraryImport(Library)]
    internal static partial byte* TessResultIteratorGetUTF8Text(nint iterator, int level);

    /// <summary>Native <c>TessResultIteratorConfidence</c>.</summary>
    /// <param name="iterator">The iterator.</param>
    /// <param name="level">The iterator level.</param>
    /// <returns>The confidence from 0 to 100.</returns>
    [LibraryImport(Library)]
    internal static partial float TessResultIteratorConfidence(nint iterator, int level);

    /// <summary>Native <c>TessResultIteratorGetPageIterator</c>.</summary>
    /// <param name="iterator">The result iterator.</param>
    /// <returns>The page iterator, owned by the result iterator.</returns>
    [LibraryImport(Library)]
    internal static partial nint TessResultIteratorGetPageIterator(nint iterator);

    /// <summary>Native <c>TessPageIteratorBoundingBox</c>.</summary>
    /// <param name="iterator">The page iterator.</param>
    /// <param name="level">The iterator level.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="right">The right edge.</param>
    /// <param name="bottom">The bottom edge.</param>
    /// <returns>Non-zero when the box exists.</returns>
    [LibraryImport(Library)]
    internal static partial int TessPageIteratorBoundingBox(nint iterator, int level, out int left, out int top, out int right, out int bottom);

    /// <summary>Native <c>TessDeleteText</c>.</summary>
    /// <param name="text">Text returned by Tesseract.</param>
    [LibraryImport(Library)]
    internal static partial void TessDeleteText(byte* text);
}
