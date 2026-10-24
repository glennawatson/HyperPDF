// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Robustness;

/// <summary>What happened to one damaged file.</summary>
/// <param name="Opened">Whether the file opened and was read in full.</param>
/// <param name="Problem">A description of a crash or hang, or <see langword="null"/>.</param>
internal readonly record struct MutantResult(bool Opened, string? Problem);
