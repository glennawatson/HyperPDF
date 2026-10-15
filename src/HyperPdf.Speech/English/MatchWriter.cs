// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Speech.English;

/// <summary>Writes the words that replace one matched piece of text.</summary>
/// <param name="context">The matched text, and where the words go.</param>
internal delegate void MatchWriter(in MatchContext context);
