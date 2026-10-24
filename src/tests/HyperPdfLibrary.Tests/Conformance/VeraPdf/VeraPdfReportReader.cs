// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Xml;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>Reads veraPDF's XML report forward-only: each job's validation result and the features it extracted.</summary>
internal static class VeraPdfReportReader
{
    /// <summary>Reads a report.</summary>
    /// <param name="xml">The report bytes.</param>
    /// <returns>The jobs by the full path of their file.</returns>
    internal static Dictionary<string, VeraPdfJob> Read(byte[] xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, IgnoreWhitespace = true };
        using var stream = new MemoryStream(xml, false);
        using var reader = XmlReader.Create(stream, settings);
        var jobs = new Dictionary<string, VeraPdfJob>(StringComparer.Ordinal);
        JobBuilder? job = null;
        while (!reader.EOF)
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "job" && job is not null)
            {
                var built = job.Build();
                jobs[built.Path] = built;
                job = null;
            }
            else if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "job")
            {
                job = new();
            }
            else if (reader.NodeType == XmlNodeType.Element && job is not null && job.Take(reader))
            {
                // Take read the element's content and left the reader on the next node.
                continue;
            }

            _ = reader.Read();
        }

        return jobs;
    }

    /// <summary>Collects one job while its elements are read.</summary>
    private sealed class JobBuilder
    {
        /// <summary>The failed rules.</summary>
        private readonly List<VeraPdfRuleFailure> _failures = [];

        /// <summary>The document information entries.</summary>
        private readonly Dictionary<string, string> _information = [with(StringComparer.Ordinal)];

        /// <summary>The page sizes.</summary>
        private readonly List<VeraPdfPage> _pages = [];

        /// <summary>The output intent condition identifiers.</summary>
        private readonly List<string> _intents = [];

        /// <summary>The file path.</summary>
        private string _path = string.Empty;

        /// <summary>The profile name.</summary>
        private string? _profile;

        /// <summary>Whether the file conforms.</summary>
        private bool _compliant;

        /// <summary>Reads an element the job needs.</summary>
        /// <param name="reader">The reader, on an element.</param>
        /// <returns><see langword="true"/> when the element's content was read, which moves the reader past it.</returns>
        internal bool Take(XmlReader reader)
        {
            switch (reader.LocalName)
            {
                case "validationReport":
                    {
                        _profile = reader.GetAttribute("profileName");
                        _compliant = reader.GetAttribute("isCompliant") == "true";
                        return false;
                    }

                case "rule" when reader.GetAttribute("status") == "failed":
                    {
                        _failures.Add(new(reader.GetAttribute("specification") ?? string.Empty, reader.GetAttribute("clause") ?? string.Empty, reader.GetAttribute("testNumber") ?? string.Empty));
                        return false;
                    }

                case "name":
                    {
                        _path = Path.GetFullPath(reader.ReadElementContentAsString());
                        return true;
                    }

                case "entry":
                    {
                        var key = reader.GetAttribute("key") ?? string.Empty;
                        _information[key] = reader.ReadElementContentAsString();
                        return true;
                    }

                default:
                    {
                        return TakeFeature(reader);
                    }
            }
        }

        /// <summary>Builds the job.</summary>
        /// <returns>The job.</returns>
        internal VeraPdfJob Build() => new(_path, _profile, _compliant, _failures, _information, _pages, _intents);

        /// <summary>Reads a page size or an output intent element.</summary>
        /// <param name="reader">The reader, on an element.</param>
        /// <returns><see langword="true"/> when the element's content was read.</returns>
        private bool TakeFeature(XmlReader reader)
        {
            switch (reader.LocalName)
            {
                case "mediaBox":
                    {
                        _pages.Add(new(Number(reader, "llx"), Number(reader, "lly"), Number(reader, "urx"), Number(reader, "ury")));
                        return false;
                    }

                case "outputConditionIdentifier":
                    {
                        _intents.Add(reader.ReadElementContentAsString());
                        return true;
                    }

                default:
                    {
                        return false;
                    }
            }

            static double Number(XmlReader reader, string attribute) =>
                double.TryParse(reader.GetAttribute(attribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
        }
    }
}
