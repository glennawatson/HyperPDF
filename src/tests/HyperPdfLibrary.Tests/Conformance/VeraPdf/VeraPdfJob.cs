// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>What veraPDF reports for one file.</summary>
/// <param name="Path">The file's full path.</param>
/// <param name="ProfileName">The validation profile used, such as "PDF/A-2b validation profile", or <see langword="null"/> when validation did not run.</param>
/// <param name="IsCompliant">Whether the file met every rule of the profile.</param>
/// <param name="Failures">The rules that failed.</param>
/// <param name="Information">The document information entries veraPDF read, by key.</param>
/// <param name="Pages">The page media boxes veraPDF read, in order.</param>
/// <param name="OutputIntents">The output intent condition identifiers veraPDF read, in order.</param>
[DebuggerDisplay("VeraPdfJob: {ProfileName} compliant {IsCompliant}")]
internal sealed record VeraPdfJob(
    string Path,
    string? ProfileName,
    bool IsCompliant,
    List<VeraPdfRuleFailure> Failures,
    Dictionary<string, string> Information,
    List<VeraPdfPage> Pages,
    List<string> OutputIntents)
{
    /// <summary>Gets the failed rules as one line, for assertion messages.</summary>
    internal string FailureSummary => string.Join("; ", Failures);
}
