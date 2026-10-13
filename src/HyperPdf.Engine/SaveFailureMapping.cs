// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdfLibrary;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine;

/// <summary>Passes the reason a security requirement stopped a save on to the app's report, so the user is told why nothing was saved.</summary>
internal static class SaveFailureMapping
{
    /// <summary>Records why a save stopped, when a security requirement stopped it, such as a PDF MAC the writer could not make.</summary>
    /// <param name="writer">The library writer's report.</param>
    /// <param name="exception">The exception the writer threw.</param>
    internal static void Note(PdfSaveReport writer, PdfException exception)
    {
        if (exception.Error == PdfError.Security)
        {
            writer.Failure = exception.Message;
        }
    }

    /// <summary>Copies the reason a save stopped to the app's report.</summary>
    /// <param name="writer">The library writer's report.</param>
    /// <param name="caller">The app's report, or <see langword="null"/>.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Copy(PdfSaveReport writer, DocumentSaveReport? caller) => caller?.Failure = writer.Failure;
}
