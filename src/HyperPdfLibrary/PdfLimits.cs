// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary;

/// <summary>Bounds that keep damaged or hostile files from exhausting memory or the stack.</summary>
internal static class PdfLimits
{
    /// <summary>The longest name kept; the specification limits names to 127 bytes.</summary>
    internal const int MaxNameLength = 127;

    /// <summary>The deepest nesting of arrays and dictionaries parsed.</summary>
    internal const int MaxNesting = 64;

    /// <summary>The longest chain of references followed.</summary>
    internal const int MaxReferenceChain = 32;

    /// <summary>The most cross-reference sections followed.</summary>
    internal const int MaxXrefSections = 512;

    /// <summary>The highest object number accepted.</summary>
    internal const int MaxObjectNumber = 8_388_607;

    /// <summary>The deepest page tree followed.</summary>
    internal const int MaxPageTreeDepth = 64;

    /// <summary>The largest decoded stream produced, guarding against decompression bombs.</summary>
    internal const int MaxDecodedLength = 1 << 28;

    /// <summary>The deepest nesting of form XObjects, patterns and Type 3 glyphs drawn.</summary>
    internal const int MaxDrawDepth = 24;

    /// <summary>The most filters applied to one stream.</summary>
    internal const int MaxFilters = 8;

    /// <summary>The largest window of a file one object or trailer is parsed from; stream data is not part of it.</summary>
    internal const int MaxObjectWindow = 1 << 26;

    /// <summary>The most requirement dictionaries read from the catalog's <c>/Requirements</c> array.</summary>
    internal const int MaxRequirements = 256;

    /// <summary>The most alternative handlers read from one requirement's <c>/RH</c> entry.</summary>
    internal const int MaxRequirementHandlers = 16;

    /// <summary>The longest script name or version text kept from a requirement, in characters.</summary>
    internal const int MaxRequirementText = 256;

    /// <summary>The largest cross-reference table section read, in bytes.</summary>
    internal const int MaxXrefWindow = 1 << 28;

    /// <summary>The longest PDF MAC token read, in bytes; a token with one recipient and three attributes is under 400.</summary>
    internal const int MaxMacTokenLength = 1 << 14;
}
