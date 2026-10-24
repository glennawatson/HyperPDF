// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Tests.Optimizing;

/// <summary>
/// The run-length code words of ITU-T Recommendation T.4 (07/2003) tables 2, 3a and 3b, read from a data file made from the
/// recommendation, and the rule that turns any run length into make-up and terminating codes (4.1.1 and the note to table 3b).
/// It is written apart from the library so the library's own tables can be checked against it.
/// </summary>
internal static class T4RunCodes
{
    /// <summary>The name of the embedded data file.</summary>
    private const string ResourceName = "HyperPdfLibrary.Tests.T4RunCodes.txt";

    /// <summary>The first run that needs a make-up code.</summary>
    private const int FirstMakeUp = 64;

    /// <summary>The longest run one make-up code covers.</summary>
    private const int LargestMakeUp = 2560;

    /// <summary>The shortest run that is coded with a make-up code of 2560 before anything else.</summary>
    private const int RepeatedMakeUpFrom = 2624;

    /// <summary>The terminating codes of white runs, by run length.</summary>
    private static readonly Dictionary<int, string> WhiteTerminating = [];

    /// <summary>The terminating codes of black runs, by run length.</summary>
    private static readonly Dictionary<int, string> BlackTerminating = [];

    /// <summary>The make-up codes of white runs, by run length, including the ones both colours share.</summary>
    private static readonly Dictionary<int, string> WhiteMakeUp = [];

    /// <summary>The make-up codes of black runs, by run length, including the ones both colours share.</summary>
    private static readonly Dictionary<int, string> BlackMakeUp = [];

    /// <summary>Reads the data file.</summary>
    static T4RunCodes()
    {
        // A missing resource leaves the tables empty, so every lookup in a test fails and says which run it was for.
        using var stream = typeof(T4RunCodes).Assembly.GetManifestResourceStream(ResourceName);
        using var reader = new StreamReader(stream ?? Stream.Null);
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > 0 && line[0] != '#')
            {
                Add(line.Split(' '));
            }
        }
    }

    /// <summary>Gets the code words for a run: make-up codes of 2560 for the part beyond 2623, a make-up code for the multiple of 64, then the terminating code.</summary>
    /// <param name="run">The run length.</param>
    /// <param name="black">Whether the run is black.</param>
    /// <returns>The code words joined into one bit string.</returns>
    internal static string Expected(int run, bool black)
    {
        var makeUp = black ? BlackMakeUp : WhiteMakeUp;
        var bits = new System.Text.StringBuilder();
        var remaining = run;
        while (remaining >= RepeatedMakeUpFrom)
        {
            _ = bits.Append(makeUp[LargestMakeUp]);
            remaining -= LargestMakeUp;
        }

        if (remaining >= FirstMakeUp)
        {
            var multiple = remaining / FirstMakeUp * FirstMakeUp;
            _ = bits.Append(makeUp[multiple]);
            remaining -= multiple;
        }

        return bits.Append((black ? BlackTerminating : WhiteTerminating)[remaining]).ToString();
    }

    /// <summary>Stores one line of the data file.</summary>
    /// <param name="fields">The colour, the run length and the code word.</param>
    private static void Add(string[] fields)
    {
        var run = int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture);
        var colour = fields[0];
        var code = fields[2];
        var forWhite = colour is "W" or "X";
        var forBlack = colour is "B" or "X";
        var terminating = run < FirstMakeUp;
        if (forWhite)
        {
            (terminating ? WhiteTerminating : WhiteMakeUp)[run] = code;
        }

        if (forBlack)
        {
            (terminating ? BlackTerminating : BlackMakeUp)[run] = code;
        }
    }
}
