// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine;

/// <summary>The reports of one save: the library writer's, and the app's that receives what the user should know.</summary>
/// <param name="Writer">The library writer's report.</param>
/// <param name="Caller">The app's report, or <see langword="null"/>.</param>
[DebuggerDisplay("SaveReports: {Writer}")]
internal readonly record struct SaveReports(PdfSaveReport Writer, DocumentSaveReport? Caller);
