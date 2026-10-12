// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Ocr.Native;

/// <summary>Owns a <c>TessBaseAPI</c> engine.</summary>
internal sealed class TesseractHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="TesseractHandle"/> class.</summary>
    public TesseractHandle()
        : base(true)
    {
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        NativeMethods.TessBaseAPIEnd(handle);
        NativeMethods.TessBaseAPIDelete(handle);
        return true;
    }
}
