// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Conformance.Isartor;

/// <summary>What the PDF/A-1 validator reported for one Isartor test file, each built to break one requirement.</summary>
internal enum IsartorOutcome
{
    /// <summary>The validator failed a rule in the clause the file was built to break.</summary>
    DetectedInClause = 0,

    /// <summary>The validator failed rules, but none in the clause the file was built to break.</summary>
    DetectedElsewhere = 1,

    /// <summary>The validator reported no failure.</summary>
    Missed = 2,

    /// <summary>The file could not be opened, so it was not validated.</summary>
    NotOpened = 3,
}
