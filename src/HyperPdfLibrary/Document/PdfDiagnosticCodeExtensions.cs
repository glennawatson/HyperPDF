// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>Helpers for <see cref="PdfDiagnosticCode"/>.</summary>
public static class PdfDiagnosticCodeExtensions
{
    /// <summary>Extension members for a diagnostic code.</summary>
    /// <param name="code">The code.</param>
    extension(PdfDiagnosticCode code)
    {
        /// <summary>Gets a value indicating whether the code reports damage that was repaired, as opposed to a limit that was reached.</summary>
        public bool IsRepair =>
            code is not (PdfDiagnosticCode.None or PdfDiagnosticCode.DecodeSizeCapped or PdfDiagnosticCode.UnknownFilter
                or PdfDiagnosticCode.RecursionLimit or PdfDiagnosticCode.FixedOnSave or PdfDiagnosticCode.BadLinearization);
    }
}
