# Interface localisation

HyperPDF ships English (`en`), French (`fr`), German (`de`), Simplified Mandarin Chinese (`zh-Hans`), Arabic (`ar`), Spanish (`es`), and Portuguese (`pt`). Portuguese uses European Portuguese terminology. Regional cultures such as `es-MX`, `pt-PT` and `pt-BR` follow their language catalog; other languages use English. The interface language is separate from the user's number and date formatting.

Choose **Interface language** in Preferences. The language names are written in their own language. Changes refresh existing windows immediately and are saved for the next launch. Open documents and edits remain in place. For a separate process with a specific language:

```powershell
hyperpdf --new-instance --language=fr document.pdf
hyperpdf --new-instance --language=de document.pdf
hyperpdf --new-instance --language=zh-Hans document.pdf
hyperpdf --new-instance --language=ar document.pdf
hyperpdf --new-instance --language=es document.pdf
hyperpdf --new-instance --language=pt document.pdf
```

Arabic windows use right-to-left layout. The PDF canvas retains its document coordinates and reading order.

## Generated resources

`HyperPdf.Localisation` consumes AppLocalisation's `Localisation.Core` and `Localisation.Generators`. Avalonia uses `Localisation.Avalonia.GeneratedResxExtension`; view models and services use `UiText.Get` and `UiText.Format`. English source text is the ordinal resource key. Static choices store source keys and translate when displayed. Formatting arguments remain separate from translated text.

Startup calls `UiText.Initialize` before Avalonia creates controls, loading the resource assembly and verifying its generated provider. This makes registration independent of which view model first requests a translated string.

Language changes flow through AppLocalisation's `InterfaceCulture.Apply(UiText, culture)` and `UiText.CultureChanges`. `LocalisationWindows` updates generated bindings, choice lists and existing view-model graphs. Retained status messages keep their resource keys and arguments; user text remains literal. Refresh updates presentation properties without reopening PDFs or repeating operations. Keep new item templates bound to the culture stream rather than copying a translated label once during construction.

Use `ChoiceViews.SetChoices` for fixed option lists. It composes AppLocalisation's `LocalisationChoices` helpers with HyperPDF's presentation style. Stable items translate their visible labels and `ToString()` representation together, preserving selection indexes, keyboard type-ahead and accessible names.

Reusable behaviour belongs to AppLocalisation: `LocalisationContext` owns lookup, culture notifications and weak choice-list tracking; `LocalisationMessage` retains deferred keys and arguments; `LocalisationStringList` supplies read-only translated labels; `ILocalisationChildren` and `LocalisationRefresh` traverse explicit graphs without reflection; `InterfaceCulture` resolves CLI/saved/desktop cultures and applies UI cultures; `LocalisationWindows`, `LocalisationBinding` and `LocalisationChoices` update existing Avalonia windows and selectors.

HyperPDF's `ResourceCatalog.UiText` supplies only its generated resource identity. Global imports expose that context and an alias for the library's message type; there are no application implementations of the generic helpers. `LanguagePreferences` connects the application's settings storage and supported-language indexes to the shared APIs. HyperPDF retains every translation, catalog and RESX file, its supported-language choices, explicit domain property notifications and child links, help composition, styling and PDF layout exceptions. Interface language changes preserve number and date formatting.

`LocalisationGenerateOnly` is enabled: the RESX files become ordinary generated C# lookup code, with no binary-resource deserialization, assembly discovery, reflection, or satellite loading. Globalization is enabled in the Native AOT executable.

The application consumes the published AppLocalisation 3.0.1 NuGet packages (`Localisation.Core`, `Localisation.Generators` and `Localisation.Avalonia`). Versions are pinned centrally in `src/Directory.Packages.props`. A normal solution restore downloads the packages; no companion checkout or local package feed is required:

```powershell
dotnet restore src/HyperPdf.slnx
```

## Updating translations

The `catalog-*.json` files hold English strings by source area. `translations.<culture>.json` and the language fragments hold translations. Preserve resource keys exactly, including case, spacing, mnemonic underscores, and every numbered format argument.

Regenerate and check complete coverage before building:

```powershell
dotnet run --file src/tools/GenerateLocalisation.cs -- --require-complete
```

The tool requires the .NET 11 SDK and uses only the .NET libraries. It preserves ordinal key ordering, translation fragment overrides, and BOM-free UTF-8 XML. Translation fragments follow the base catalog in case-insensitive current-culture filename order, matching the previous PowerShell tool. Without `--require-complete`, unavailable languages are skipped and missing translations use English. Strict mode validates every supported language before writing any resource files.

The generated RESX files are checked in so normal builds do not run a translation tool. No runtime network translation service is used.

## Verification

The `Localisation` TUnit category checks known translations, regional fallback, all catalog keys and format arguments, startup language precedence, right-to-left layout, and actual main, preferences and print window text bounds in every shipped language. `UiTextBenchmarks` compares source literals with generated lookup using the repository's EventPipe allocation harness.

```powershell
Push-Location ./src
dotnet test --project "$PWD/tests/HyperPdf.App.Tests/HyperPdf.App.Tests.csproj" -f net11.0 -- --treenode-filter '/*/*/*/*[Category=Localisation]'
dotnet test --project "$PWD/tests/HyperPdf.ViewModels.Tests/HyperPdf.ViewModels.Tests.csproj" -f net11.0 -- --treenode-filter '/*/*/*/*[Category=LiveCulture]'
Pop-Location
dotnet publish src/HyperPdf.App/HyperPdf.App.csproj -c Release -f net11.0 -r win-x64 -o artifacts/localisation-native
```

Run the published executable in each language; a normal managed build alone does not verify Native AOT.
