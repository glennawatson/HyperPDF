// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>A font rule of Well-Tagged PDF that a document breaks.</summary>
/// <param name="Clause">The Well-Tagged PDF clause.</param>
/// <param name="Description">What the rule requires.</param>
internal readonly record struct WtpdfFontFailure(string Clause, string Description);
