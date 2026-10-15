// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Creation;

/// <summary>How a <see cref="PdfFlowCodeBlock"/> handles a line that is wider than the available inner width.</summary>
public enum PdfCodeOverflow
{
    /// <summary>Wraps at token boundaries when possible, falling back to character boundaries for tokens wider than the line.</summary>
    Wrap = 0,

    /// <summary>Wraps strictly at character (text-element) boundaries so each wrapped line fills the width before breaking.</summary>
    WrapCharacters = 1,

    /// <summary>Truncates the line at the available width without wrapping onto a continuation line.</summary>
    Clip = 2,
}
