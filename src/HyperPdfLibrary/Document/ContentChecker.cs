// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Parses a page's content and reports unknown operators, a restore with nothing saved, and text objects left open.</summary>
internal static class ContentChecker
{
    /// <summary>Scans decoded content.</summary>
    /// <param name="content">The decoded content.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="pageNumber">The page object's number, or 0.</param>
    /// <param name="faults">The list receiving each fault; the offset is the position in the decoded content.</param>
    internal static void Scan(ReadOnlySpan<byte> content, PdfNameTable names, int pageNumber, List<PdfDiagnostic> faults)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, names, operands);
        var state = default(ScanState);
        var more = true;
        while (more)
        {
            var position = reader.Position;
            more = reader.Next(out var op);
            if (more)
            {
                state.Step(op, pageNumber, position, faults);
            }
        }

        if (state.IsTextOpen)
        {
            faults.Add(new(PdfDiagnosticCode.BadContentStream, "A text object (BT) is never closed.", pageNumber, content.Length));
        }
    }

    /// <summary>The nesting the scan tracks.</summary>
    private struct ScanState
    {
        /// <summary>The graphics states saved and not restored.</summary>
        private int _savedStates;

        /// <summary>The compatibility sections (BX) not yet closed; unknown operators inside them are allowed.</summary>
        private int _compatibility;

        /// <summary>Whether a text object (BT) is open.</summary>
        private bool _textOpen;

        /// <summary>Gets a value indicating whether a text object is still open.</summary>
        public readonly bool IsTextOpen => _textOpen;

        /// <summary>Applies one operator.</summary>
        /// <param name="op">The operator.</param>
        /// <param name="pageNumber">The page object's number.</param>
        /// <param name="position">The operator's offset in the content.</param>
        /// <param name="faults">The fault list.</param>
        public void Step(ContentOperator op, int pageNumber, int position, List<PdfDiagnostic> faults)
        {
            switch (op)
            {
                case ContentOperator.Save:
                {
                    _savedStates++;
                    break;
                }

                case ContentOperator.Restore:
                {
                    Restore(pageNumber, position, faults);
                    break;
                }

                case ContentOperator.BeginText:
                {
                    _textOpen = true;
                    break;
                }

                case ContentOperator.EndText:
                {
                    _textOpen = false;
                    break;
                }

                case ContentOperator.BeginCompatibility:
                {
                    _compatibility++;
                    break;
                }

                case ContentOperator.EndCompatibility:
                {
                    _compatibility = Math.Max(0, _compatibility - 1);
                    break;
                }

                case ContentOperator.Unknown when _compatibility == 0:
                {
                    faults.Add(new(PdfDiagnosticCode.BadContentStream, "The content has an operator that is not defined.", pageNumber, position));
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        /// <summary>Handles a restore, which needs a matching save.</summary>
        /// <param name="pageNumber">The page object's number.</param>
        /// <param name="position">The operator's offset.</param>
        /// <param name="faults">The fault list.</param>
        private void Restore(int pageNumber, int position, List<PdfDiagnostic> faults)
        {
            if (_savedStates == 0)
            {
                faults.Add(new(PdfDiagnosticCode.BadContentStream, "The content restores a graphics state (Q) that was never saved.", pageNumber, position));
                return;
            }

            _savedStates--;
        }
    }
}
