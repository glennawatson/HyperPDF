// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Ocr.Native;

/// <summary>Owns a <c>TessResultIterator</c>.</summary>
internal sealed class TesseractIteratorHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="TesseractIteratorHandle"/> class.</summary>
    public TesseractIteratorHandle()
        : base(true)
    {
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        NativeMethods.TessResultIteratorDelete(handle);
        return true;
    }
}
