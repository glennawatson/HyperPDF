// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.App.ViewModels;

/// <summary>The outcome of recognising a document's pages.</summary>
/// <param name="Recognized">Pages given a text layer.</param>
/// <param name="AlreadyText">Pages that already had text.</param>
/// <param name="Closed">Whether the document closed part way.</param>
/// <param name="Unsure">Whether the first scanned page read too poorly to trust, so the run stopped before writing anything.</param>
[DebuggerDisplay("RecognitionRun: {Recognized} recognised, {AlreadyText} had text")]
internal readonly record struct RecognitionRun(int Recognized, int AlreadyText, bool Closed, bool Unsure);
