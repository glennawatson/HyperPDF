// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Ocr;

/// <summary>The result of recognising one page.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Status">What happened.</param>
/// <param name="Words">The number of words written.</param>
[DebuggerDisplay("OcrPageResult: Page {PageIndex}: {Status} ({Words})")]
public readonly record struct OcrPageResult(int PageIndex, OcrPageStatus Status, int Words);
