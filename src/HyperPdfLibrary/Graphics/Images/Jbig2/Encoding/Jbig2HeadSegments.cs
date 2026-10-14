// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

/// <summary>The segment numbers of a symbol-coded page once its page information and dictionaries are written.</summary>
/// <param name="DictionaryNumber">The base symbol dictionary's segment number.</param>
/// <param name="RefinedNumber">The refined symbol dictionary's segment number, or -1 when there is none.</param>
/// <param name="NextNumber">The next free segment number in the page stream.</param>
internal readonly record struct Jbig2HeadSegments(int DictionaryNumber, int RefinedNumber, int NextNumber);
