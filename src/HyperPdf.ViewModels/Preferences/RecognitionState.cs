// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Ocr;

namespace HyperPdf.ViewModels.Preferences;

/// <summary>What one text recognition run has done so far.</summary>
/// <param name="checkFirst">Whether the first recognised page is checked for how well it reads before anything is written.</param>
internal sealed class RecognitionState(bool checkFirst)
{
    /// <summary>Gets the reusable list the words of a page are read into.</summary>
    internal List<OcrWord> Words { get; } = [];

    /// <summary>Gets or sets a value indicating whether the next page is checked for how well it reads.</summary>
    internal bool CheckFirst { get; set; } = checkFirst;

    /// <summary>Gets or sets a value indicating whether the run ended early: the document closed or the first page read poorly.</summary>
    internal bool Done { get; set; }

    /// <summary>Gets or sets what was recognised.</summary>
    internal RecognitionRun Run { get; set; }
}
