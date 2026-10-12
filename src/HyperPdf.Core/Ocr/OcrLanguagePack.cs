// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Ocr;

/// <summary>A Tesseract language pack: one <c>.traineddata</c> file that teaches the recogniser a language or script.</summary>
/// <param name="Code">The Tesseract language code, for example <c>eng</c> or <c>chi_sim</c>.</param>
/// <param name="Name">The language's name for people, for example "German (Deutsch)".</param>
/// <param name="Bytes">The file's size in bytes; a file of another size is downloaded again.</param>
/// <param name="Sha256">The file's SHA-256 as lowercase hexadecimal; a download that does not match is discarded.</param>
[DebuggerDisplay("OcrLanguagePack: {Code} {Name}")]
public sealed record OcrLanguagePack(string Code, string Name, long Bytes, string Sha256)
{
    /// <summary>The extension Tesseract expects on a language file.</summary>
    private const string Extension = ".traineddata";

    /// <summary>Gets the file name Tesseract looks for, for example <c>eng.traineddata</c>.</summary>
    public string FileName { get; } = Code + Extension;

    /// <summary>Gets where the pack is downloaded from.</summary>
    public Uri Source { get; } = new(OcrLanguageCatalog.BaseAddress, Code + Extension);
}
