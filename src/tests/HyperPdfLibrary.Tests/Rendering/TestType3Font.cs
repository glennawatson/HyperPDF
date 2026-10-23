// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>A one-byte Type 3 font whose every code draws the /a procedure of /CharProcs, one em wide.</summary>
[DebuggerDisplay("TestType3Font")]
internal sealed class TestType3Font : PdfType3Font
{
    /// <summary>Initializes a new instance of the <see cref="TestType3Font"/> class.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    internal TestType3Font(PdfDictionary dictionary)
        : base(dictionary)
    {
    }

    /// <inheritdoc/>
    public override PdfDictionary? Resources => Dictionary.GetDictionary(KnownName.Resources);

    /// <inheritdoc/>
    public override PdfStream? GetCharProc(int code)
    {
        var procedures = Dictionary.GetDictionary(KnownName.CharProcs);
        return procedures is { Count: > 0 } ? procedures.GetStream(procedures.GetKeyAt(0)) : null;
    }

    /// <inheritdoc/>
    public override int ReadCode(ReadOnlySpan<byte> bytes, out int code)
    {
        code = bytes[0];
        return 1;
    }

    /// <inheritdoc/>
    public override float GetWidth(int code) => 1;

    /// <inheritdoc/>
    public override int GetUnicode(int code, Span<char> destination)
    {
        destination[0] = (char)code;
        return 1;
    }
}
