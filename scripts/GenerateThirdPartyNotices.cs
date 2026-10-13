#!/usr/bin/env -S dotnet run --file
#:property IsAotCompatible=false
#:property PublishAot=false

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Writes THIRD-PARTY-NOTICES.md from the resolved NuGet graph (project.assets.json files plus the local package cache,
// with no network access) and the components listed in licenses/components.json that are bundled with the app or
// downloaded by it. Every shipped package appears with its licence text, transitive packages included.
// Shared app notices require --assets inputs for every supported runtime, including each native OCR package;
// one host's restore graph omits the other platforms. Repeated --assets inputs are merged by package and version.
// Usage: dotnet run --file scripts/GenerateThirdPartyNotices.cs -- --out <file> --licences <licenses folder>
//        --project-licence <LICENSE file> [--assets <project.assets.json>]... [--component <id>]...
using System.Text;
using System.Text.Json;
using System.Xml;

return HyperPdf.Scripts.GenerateThirdPartyNotices.Run(args);

namespace HyperPdf.Scripts
{
    /// <summary>Builds the third-party notices file.</summary>
    internal static class GenerateThirdPartyNotices
    {
        /// <summary>The exit code when the file was written.</summary>
        private const int Success = 0;

        /// <summary>The exit code when the arguments are wrong.</summary>
        private const int UsageError = 2;

        /// <summary>The exit code when a package has no licence the tool can read.</summary>
        private const int MissingLicence = 3;

        /// <summary>The origin text of this application's own entry.</summary>
        private const string ThisApplication = "This application";

        /// <summary>The fence that wraps licence text; longer than any run of backticks in a licence.</summary>
        private const string Fence = "````";

        /// <summary>Runs the tool.</summary>
        /// <param name="args">The command line.</param>
        /// <returns>The exit code.</returns>
        internal static int Run(string[] args)
        {
            var options = Options.Parse(args);
            if (options is null)
            {
                Console.Error.WriteLine("Usage: GenerateThirdPartyNotices --out <file> --licences <folder> --project-licence <file> [--assets <file>]... [--component <id>]...");
                return UsageError;
            }

            SpdxTexts.Folder = Path.Combine(options.Licences, "spdx");
            var overrides = Overrides.Load(Path.Combine(options.Licences, "packages.json"));
            var components = new List<Component> { ProjectComponent(options) };
            var problems = new List<string>();
            AddPackages(options, overrides, components, problems);
            components.AddRange(ComponentFile.Load(Path.Combine(options.Licences, "components.json"), options.Components));
            foreach (var problem in problems)
            {
                Console.Error.WriteLine(problem);
            }

            WriteIfChanged(options.Out, Render(components));
            return problems.Count == 0 ? Success : MissingLicence;
        }

        /// <summary>Makes the entry for this application's own licence.</summary>
        /// <param name="options">The options.</param>
        /// <returns>The component.</returns>
        private static Component ProjectComponent(Options options)
        {
            var text = File.ReadAllText(options.ProjectLicence).Trim();
            var copyright = Array.Find(text.Split('\n'), static line => line.StartsWith("Copyright", StringComparison.Ordinal))?.Trim() ?? string.Empty;
            return new(options.ProjectName, string.Empty, ThisApplication, "MIT", copyright, "https://github.com/glennawatson/HyperPDF", text);
        }

        /// <summary>Adds every shipped package of the assets files.</summary>
        /// <param name="options">The options.</param>
        /// <param name="overrides">The licence overrides.</param>
        /// <param name="components">The list to add to.</param>
        /// <param name="problems">Receives a message for each package without a usable licence.</param>
        private static void AddPackages(Options options, Overrides overrides, List<Component> components, List<string> problems)
        {
            var packages = new SortedDictionary<string, PackageRef>(StringComparer.OrdinalIgnoreCase);
            foreach (var assets in options.Assets)
            {
                foreach (var package in AssetsFile.ShippedPackages(assets))
                {
                    packages[$"{package.Id}/{package.Version}"] = package;
                }
            }

            foreach (var package in packages.Values)
            {
                if (PackageReader.Read(package, overrides) is { } component)
                {
                    components.Add(component);
                }
                else
                {
                    problems.Add($"error: {package.Id} {package.Version} has no licence the tool can read; add it to licenses/packages.json.");
                }
            }
        }

        /// <summary>Renders the notices: components grouped by licence, this application first.</summary>
        /// <param name="components">The components.</param>
        /// <returns>The file text.</returns>
        private static string Render(List<Component> components)
        {
            var groups = new SortedDictionary<string, List<Component>>(StringComparer.Ordinal);
            foreach (var component in components)
            {
                if (!groups.TryGetValue(component.Licence, out var list))
                {
                    list = [];
                    groups[component.Licence] = list;
                }

                list.Add(component);
            }

            var text = new StringBuilder();
            _ = text.Append("# Third-party notices\n\n");
            _ = text.Append(components[0].Name).Append(" is released under the MIT licence below. It includes or downloads the components listed after it, grouped by licence.\n");
            foreach (var licence in GroupOrder(groups))
            {
                _ = text.Append("\n## ").Append(licence).Append('\n');
                var list = groups[licence];
                list.Sort(static (a, b) => ComponentOrder(a).CompareTo(ComponentOrder(b)) is var byKind and not 0 ? byKind : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
                foreach (var component in list)
                {
                    AppendComponent(text, component);
                }
            }

            return text.ToString();
        }

        /// <summary>Puts the group that holds this application first and keeps the others in name order.</summary>
        /// <param name="groups">The groups by licence.</param>
        /// <returns>The licence names in output order.</returns>
        private static List<string> GroupOrder(SortedDictionary<string, List<Component>> groups)
        {
            var order = new List<string>(groups.Keys);
            var index = order.FindIndex(licence => groups[licence].Exists(static c => c.Origin == ThisApplication));
            if (index > 0)
            {
                var own = order[index];
                order.RemoveAt(index);
                order.Insert(0, own);
            }

            return order;
        }

        /// <summary>Sorts this application's own entry before the rest of its group.</summary>
        /// <param name="component">The component.</param>
        /// <returns>0 for this application, 1 for the rest.</returns>
        private static int ComponentOrder(Component component) => component.Origin == ThisApplication ? 0 : 1;

        /// <summary>Appends one component.</summary>
        /// <param name="text">The output.</param>
        /// <param name="component">The component.</param>
        private static void AppendComponent(StringBuilder text, Component component)
        {
            _ = text.Append("\n### ").Append(component.Name).Append('\n');
            AppendField(text, nameof(Component.Version), component.Version);
            AppendField(text, nameof(Component.Origin), component.Origin);
            AppendField(text, nameof(Component.Copyright), component.Copyright);
            AppendField(text, nameof(Component.Link), component.Link);
            _ = text.Append('\n').Append(Fence).Append("text\n");
            foreach (var line in component.Text.ReplaceLineEndings("\n").Trim().AsSpan().EnumerateLines())
            {
                _ = text.Append(line.TrimEnd()).Append('\n');
            }

            _ = text.Append(Fence).Append('\n');
        }

        /// <summary>Appends a field line when it has a value.</summary>
        /// <param name="text">The output.</param>
        /// <param name="name">The field name.</param>
        /// <param name="value">The value.</param>
        private static void AppendField(StringBuilder text, string name, string value)
        {
            if (value.Length > 0)
            {
                _ = text.Append("- ").Append(name).Append(": ").Append(value.ReplaceLineEndings(" ").Trim()).Append('\n');
            }
        }

        /// <summary>Writes a file only when its content changed, so incremental builds keep the timestamp.</summary>
        /// <param name="file">The path.</param>
        /// <param name="content">The content.</param>
        private static void WriteIfChanged(string file, string content)
        {
            if (File.Exists(file) && File.ReadAllText(file) == content)
            {
                return;
            }

            var folder = Path.GetDirectoryName(Path.GetFullPath(file));
            if (!string.IsNullOrEmpty(folder))
            {
                _ = Directory.CreateDirectory(folder);
            }

            var temporary = $"{file}.{Environment.ProcessId}.tmp";
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, file, true);
        }

        /// <summary>Reads project.assets.json files.</summary>
        internal static class AssetsFile
        {
            /// <summary>Lists the packages a project ships: those with runtime, native or resource assets in some target.</summary>
            /// <param name="file">The project.assets.json path.</param>
            /// <returns>The packages.</returns>
            internal static List<PackageRef> ShippedPackages(string file)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                var root = document.RootElement;
                var folders = new List<string>();
                foreach (var folder in root.GetProperty("packageFolders").EnumerateObject())
                {
                    folders.Add(folder.Name);
                }

                var shipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var target in root.GetProperty("targets").EnumerateObject())
                {
                    foreach (var entry in target.Value.EnumerateObject())
                    {
                        if (Ships(entry.Value))
                        {
                            _ = shipped.Add(entry.Name);
                        }
                    }
                }

                var result = new List<PackageRef>();
                foreach (var library in root.GetProperty("libraries").EnumerateObject())
                {
                    if (library.Value.GetProperty("type").GetString() == "package" && shipped.Contains(library.Name))
                    {
                        result.Add(MakeRef(library, folders));
                    }
                }

                return result;
            }

            /// <summary>Makes the package reference of a library entry.</summary>
            /// <param name="library">The library property.</param>
            /// <param name="folders">The package folders.</param>
            /// <returns>The reference.</returns>
            private static PackageRef MakeRef(JsonProperty library, List<string> folders)
            {
                var split = library.Name.IndexOf('/', StringComparison.Ordinal);
                var relative = library.Value.GetProperty("path").GetString() ?? string.Empty;
                var folder = folders.Find(f => Directory.Exists(Path.Combine(f, relative))) ?? folders[0];
                return new(library.Name[..split], library.Name[(split + 1)..], Path.Combine(folder, relative));
            }

            /// <summary>Determines whether a target entry carries files that end up beside the app.</summary>
            /// <param name="entry">The target entry.</param>
            /// <returns><see langword="true"/> when it has runtime, native, runtime-target or resource assets.</returns>
            private static bool Ships(JsonElement entry) =>
                entry.TryGetProperty("runtime", out _) || entry.TryGetProperty("native", out _) || entry.TryGetProperty("runtimeTargets", out _) || entry.TryGetProperty("resource", out _);
        }

        /// <summary>Reads one package's licence from its nuspec and files.</summary>
        internal static class PackageReader
        {
            /// <summary>The names of the licence files looked for in a package, in order.</summary>
            private static readonly string[] LicenceFileNames = ["LICENSE", "LICENSE.txt", "LICENSE.md", "LICENCE", "LICENCE.txt", "COPYING", "COPYING.txt", "LICENSE-MIT", "LICENSE.MIT"];

            /// <summary>Reads a package.</summary>
            /// <param name="package">The package.</param>
            /// <param name="overrides">The corrections.</param>
            /// <returns>The component, or <see langword="null"/> when no licence can be found.</returns>
            internal static Component? Read(PackageRef package, Overrides overrides)
            {
                var meta = Nuspec.Read(package);
                _ = overrides.ById.TryGetValue(package.Id, out var fix);
                var text = fix is { TextFile.Length: > 0 }
                    ? File.ReadAllText(Path.Combine(overrides.Root, fix.TextFile))
                    : FindText(package, meta);
                var expression = fix is { Licence.Length: > 0 } ? fix.Licence : meta.Expression;
                if (expression.Length == 0 && text is not null)
                {
                    expression = LicenceClassifier.Classify(text);
                }

                text ??= expression.Length > 0 ? SpdxTexts.For(expression) : null;
                if (text is null || expression.Length == 0)
                {
                    return null;
                }

                var copyright = fix is { Copyright.Length: > 0 } ? fix.Copyright : meta.Copyright;
                return new(package.Id, package.Version, "NuGet package", expression, copyright, meta.ProjectUrl, text + BundledLicences(package.Folder));
            }

            /// <summary>Reads the licences of the libraries inside a package, from the package's licenses folder.</summary>
            /// <param name="folder">The package folder.</param>
            /// <returns>The texts, each under a heading with its file name, or empty when the package has none.</returns>
            private static string BundledLicences(string folder)
            {
                var licences = Path.Combine(folder, "licenses");
                if (!Directory.Exists(licences))
                {
                    return string.Empty;
                }

                var files = new List<string>(Directory.EnumerateFiles(licences));
                files.Sort(StringComparer.Ordinal);
                var text = new StringBuilder();
                foreach (var file in files)
                {
                    _ = text.Append("\n\n----- Library licence: ").Append(Path.GetFileNameWithoutExtension(file)).Append(" -----\n\n").Append(File.ReadAllText(file).Trim());
                }

                return text.ToString();
            }

            /// <summary>Finds the licence text: the file the nuspec names, a licence file in the package, then the SPDX text.</summary>
            /// <param name="package">The package.</param>
            /// <param name="meta">The nuspec metadata.</param>
            /// <returns>The text, or <see langword="null"/>.</returns>
            private static string? FindText(PackageRef package, Nuspec meta)
            {
                if (meta.LicenceFile.Length > 0 && File.Exists(Path.Combine(package.Folder, meta.LicenceFile)))
                {
                    return File.ReadAllText(Path.Combine(package.Folder, meta.LicenceFile));
                }

                if (FindLicenceFile(package.Folder) is { } found)
                {
                    return File.ReadAllText(found);
                }

                return meta.Expression.Length > 0 ? SpdxTexts.For(meta.Expression) : null;
            }

            /// <summary>Finds a licence file in the root of a package folder, the first in name order.</summary>
            /// <param name="folder">The package folder.</param>
            /// <returns>The path, or <see langword="null"/>.</returns>
            private static string? FindLicenceFile(string folder)
            {
                var matches = new List<string>();
                if (Directory.Exists(folder))
                {
                    foreach (var candidate in Directory.EnumerateFiles(folder))
                    {
                        var name = Path.GetFileName(candidate);
                        if (Array.Exists(LicenceFileNames, known => string.Equals(known, name, StringComparison.OrdinalIgnoreCase)))
                        {
                            matches.Add(candidate);
                        }
                    }
                }

                matches.Sort(StringComparer.Ordinal);
                return matches.Count > 0 ? matches[0] : null;
            }
        }

        /// <summary>Works out an SPDX id from the words of a licence text.</summary>
        internal static class LicenceClassifier
        {
            /// <summary>Classifies a licence text.</summary>
            /// <param name="text">The text.</param>
            /// <returns>The SPDX id, or empty when the text is not one the tool knows.</returns>
            internal static string Classify(string text)
            {
                if (text.Contains("Permission is hereby granted, free of charge", StringComparison.Ordinal))
                {
                    return "MIT";
                }

                if (text.Contains("Apache License", StringComparison.Ordinal) && text.Contains("Version 2.0", StringComparison.Ordinal))
                {
                    return "Apache-2.0";
                }

                if (text.Contains("Redistribution and use in source and binary forms", StringComparison.Ordinal))
                {
                    return text.Contains("Neither the name", StringComparison.Ordinal) ? "BSD-3-Clause" : "BSD-2-Clause";
                }

                return ClassifyOther(text);
            }

            /// <summary>Classifies the less common licences.</summary>
            /// <param name="text">The text.</param>
            /// <returns>The SPDX id, or empty.</returns>
            private static string ClassifyOther(string text)
            {
                if (text.Contains("SIL OPEN FONT LICENSE", StringComparison.OrdinalIgnoreCase))
                {
                    return "OFL-1.1";
                }

                if (text.Contains("Microsoft Public License", StringComparison.Ordinal))
                {
                    return "MS-PL";
                }

                if (text.Contains("Permission to use, copy, modify, and/or distribute", StringComparison.Ordinal))
                {
                    return "ISC";
                }

                return text.Contains("provided 'as-is', without any express or implied warranty", StringComparison.Ordinal) ? "Zlib" : string.Empty;
            }
        }

        /// <summary>The checked-in SPDX licence texts, for packages that give only an expression.</summary>
        internal static class SpdxTexts
        {
            /// <summary>Gets or sets the folder that holds the SPDX texts.</summary>
            internal static string Folder { get; set; } = string.Empty;

            /// <summary>Gets the text of an expression: each id's text, one after another.</summary>
            /// <param name="expression">The SPDX expression, such as <c>MIT</c> or <c>MIT AND Apache-2.0</c>.</param>
            /// <returns>The text, or <see langword="null"/> when an id has no checked-in text.</returns>
            internal static string? For(string expression)
            {
                var texts = new List<string>();
                foreach (var id in expression.Split([' ', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
                {
                    if (id is "AND" or "OR" or "WITH")
                    {
                        continue;
                    }

                    var file = Path.Combine(Folder, $"{id}.txt");
                    if (!File.Exists(file))
                    {
                        return null;
                    }

                    texts.Add(File.ReadAllText(file).Trim());
                }

                return texts.Count == 0 ? null : string.Join("\n\n", texts);
            }
        }

        /// <summary>Reads components.json: the licences of what is bundled or downloaded outside NuGet.</summary>
        internal static class ComponentFile
        {
            /// <summary>Loads the components with the wanted ids.</summary>
            /// <param name="file">The components.json path.</param>
            /// <param name="ids">The ids to include.</param>
            /// <returns>The components.</returns>
            internal static List<Component> Load(string file, List<string> ids)
            {
                var result = new List<Component>();
                if (ids.Count == 0 || !File.Exists(file))
                {
                    return result;
                }

                var folder = Path.GetDirectoryName(Path.GetFullPath(file)) ?? string.Empty;
                using var document = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var entry in document.RootElement.EnumerateArray())
                {
                    if (ids.Contains(Json.Read(entry, "id")) || ids.Contains(Json.Read(entry, "bundle")))
                    {
                        result.Add(Make(entry, folder));
                    }
                }

                return result;
            }

            /// <summary>Makes a component from its JSON object.</summary>
            /// <param name="entry">The object.</param>
            /// <param name="folder">The folder the text file is relative to.</param>
            /// <returns>The component.</returns>
            private static Component Make(JsonElement entry, string folder) => new(
                Json.Read(entry, "name"),
                Json.Read(entry, "version"),
                Json.Read(entry, "origin"),
                Json.Read(entry, "licence"),
                Json.Read(entry, "copyright"),
                Json.Read(entry, "link"),
                File.ReadAllText(Path.Combine(folder, Json.Read(entry, "textFile"))));
        }

        /// <summary>Small JSON helpers.</summary>
        internal static class Json
        {
            /// <summary>Reads an optional string property.</summary>
            /// <param name="element">The object.</param>
            /// <param name="name">The property name.</param>
            /// <returns>The value, or empty.</returns>
            internal static string Read(JsonElement element, string name) =>
                element.TryGetProperty(name, out var value) ? value.GetString() ?? string.Empty : string.Empty;
        }

        /// <summary>The command line options.</summary>
        /// <param name="Out">The output file.</param>
        /// <param name="Licences">The licences folder.</param>
        /// <param name="ProjectLicence">The application's own LICENSE file.</param>
        /// <param name="Assets">The project.assets.json files.</param>
        /// <param name="Components">The ids of the bundled components to include; empty means none.</param>
        internal sealed record Options(string Out, string Licences, string ProjectLicence, List<string> Assets, List<string> Components)
        {
            /// <summary>The arguments each option takes: its name and its value.</summary>
            private const int OptionWidth = 2;

            /// <summary>Gets the name of the project whose notices are generated.</summary>
            internal string ProjectName { get; private init; } = "HyperPDF";

            /// <summary>Parses the arguments.</summary>
            /// <param name="args">The arguments.</param>
            /// <returns>The options, or <see langword="null"/> when a required one is missing.</returns>
            internal static Options? Parse(string[] args)
            {
                string? output = null;
                string? licences = null;
                string? projectLicence = null;
                var projectName = "HyperPDF";
                var assets = new List<string>();
                var components = new List<string>();
                for (var i = 0; i + 1 < args.Length; i += OptionWidth)
                {
                    var value = args[i + 1];
                    switch (args[i])
                    {
                        case "--out":
                        {
                            output = value;
                            break;
                        }

                        case "--licences":
                        {
                            licences = value;
                            break;
                        }

                        case "--project-licence":
                        {
                            projectLicence = value;
                            break;
                        }

                        case "--project-name":
                        {
                            projectName = value;
                            break;
                        }

                        case "--assets":
                        {
                            assets.Add(value);
                            break;
                        }

                        case "--component":
                        {
                            components.Add(value);
                            break;
                        }

                        default:
                        {
                            return null;
                        }
                    }
                }

                return output is null || licences is null || projectLicence is null ? null : new(output, licences, projectLicence, assets, components) { ProjectName = projectName };
            }
        }

        /// <summary>One entry of the notices.</summary>
        /// <param name="Name">The name.</param>
        /// <param name="Version">The version, or empty.</param>
        /// <param name="Origin">Where the component comes from.</param>
        /// <param name="Licence">The SPDX licence expression.</param>
        /// <param name="Copyright">The copyright notice, or empty.</param>
        /// <param name="Link">A link to the project, or empty.</param>
        /// <param name="Text">The full licence text.</param>
        internal sealed record Component(string Name, string Version, string Origin, string Licence, string Copyright, string Link, string Text);

        /// <summary>A package of the resolved graph.</summary>
        /// <param name="Id">The package id.</param>
        /// <param name="Version">The version.</param>
        /// <param name="Folder">The folder the package is unpacked to.</param>
        internal sealed record PackageRef(string Id, string Version, string Folder);

        /// <summary>Hand-written corrections for packages whose own metadata is not enough.</summary>
        /// <param name="ById">The corrections by package id.</param>
        /// <param name="Root">The folder that text file paths are relative to.</param>
        internal sealed record Overrides(Dictionary<string, PackageOverride> ById, string Root)
        {
            /// <summary>Loads the overrides file.</summary>
            /// <param name="file">The path.</param>
            /// <returns>The overrides; empty when the file is missing.</returns>
            internal static Overrides Load(string file)
            {
                var byId = new Dictionary<string, PackageOverride>(StringComparer.OrdinalIgnoreCase);
                var root = Path.GetDirectoryName(Path.GetFullPath(file)) ?? string.Empty;
                if (!File.Exists(file))
                {
                    return new(byId, root);
                }

                using var document = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var entry in document.RootElement.EnumerateObject())
                {
                    byId[entry.Name] = new(Json.Read(entry.Value, "licence"), Json.Read(entry.Value, "copyright"), Json.Read(entry.Value, "textFile"));
                }

                return new(byId, root);
            }
        }

        /// <summary>A correction for one package.</summary>
        /// <param name="Licence">The SPDX expression, or empty.</param>
        /// <param name="Copyright">The copyright notice, or empty.</param>
        /// <param name="TextFile">A licence text file relative to the licences folder, or empty.</param>
        internal sealed record PackageOverride(string Licence, string Copyright, string TextFile);

        /// <summary>The licence fields of a nuspec.</summary>
        /// <param name="Expression">The SPDX expression, or empty.</param>
        /// <param name="LicenceFile">The licence file named by the nuspec, or empty.</param>
        /// <param name="Copyright">The copyright notice.</param>
        /// <param name="ProjectUrl">The project URL.</param>
        internal sealed record Nuspec(string Expression, string LicenceFile, string Copyright, string ProjectUrl)
        {
            /// <summary>Reads a package's nuspec with a forward-only reader.</summary>
            /// <param name="package">The package.</param>
            /// <returns>The fields.</returns>
            internal static Nuspec Read(PackageRef package)
            {
                var file = Path.Combine(package.Folder, $"{package.Id.ToLowerInvariant()}.nuspec");
                if (!File.Exists(file))
                {
                    return new(string.Empty, string.Empty, string.Empty, string.Empty);
                }

                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using var reader = XmlReader.Create(file, settings);
                var expression = string.Empty;
                var licenceFile = string.Empty;
                var copyright = string.Empty;
                var url = string.Empty;
                while (reader.Read())
                {
                    if (reader.NodeType != XmlNodeType.Element)
                    {
                        continue;
                    }

                    switch (reader.LocalName)
                    {
                        case "license":
                        {
                            var isFile = reader.GetAttribute("type") == "file";
                            var value = reader.ReadElementContentAsString().Trim();
                            expression = isFile ? expression : value;
                            licenceFile = isFile ? value : licenceFile;
                            break;
                        }

                        case "copyright":
                        {
                            copyright = reader.ReadElementContentAsString().Trim();
                            break;
                        }

                        case "projectUrl":
                        {
                            url = reader.ReadElementContentAsString().Trim();
                            break;
                        }

                        default:
                        {
                            break;
                        }
                    }
                }

                return new(expression, licenceFile, copyright, url);
            }
        }
    }
}
