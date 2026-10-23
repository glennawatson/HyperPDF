// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>How a composition is laid out.</summary>
[DebuggerDisplay("PdfComposeOptions")]
public sealed record PdfComposeOptions
{
    /// <summary>Gets the options that change nothing.</summary>
    public static PdfComposeOptions Default { get; } = new();

    /// <summary>Gets the clock that dates <see cref="PdfFieldKind.Date"/> fields, or <see langword="null"/> for the system clock. Give a fixed clock to get the same file every time.</summary>
    public TimeProvider? Clock { get; init; }
}
