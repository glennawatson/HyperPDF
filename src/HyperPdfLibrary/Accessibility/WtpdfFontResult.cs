// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>The font rules of Well-Tagged PDF a document broke, for one revision of its objects.</summary>
/// <param name="Revision">The revision of the document's objects the rules were checked at; an edit makes the result stale.</param>
/// <param name="Failures">The rules broken, each as the clause and the description of the rule.</param>
internal sealed record WtpdfFontResult(int Revision, WtpdfFontFailure[] Failures);
