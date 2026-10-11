// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.App.ViewModels;

/// <summary>The result of writing and publishing a document.</summary>
/// <param name="Saved">Whether the destination was replaced.</param>
/// <param name="Error">A file-system error to show to the reader, or <see langword="null"/>.</param>
internal readonly record struct DocumentSaveOutcome(bool Saved, string? Error);
