# Bug reports

Search [open and closed issues](https://github.com/glennawatson/HyperPDF/issues) before filing a report. Use the [bug form](https://github.com/glennawatson/HyperPDF/issues/new?template=bugs.yml). Keep one fault in each issue.

Name the trigger and the fault in the title. For example, `[Bug]: Print preview moves when scrolling`.

Fill each field in this order:

| Field | What to write |
| --- | --- |
| App version | The release version or commit. Write `Unknown` when it is unavailable. |
| Environment | Operating system, version, architecture and package format. Add the screen reader or desktop session when relevant. |
| Steps to reproduce | Numbered steps. Name the controls used. State how often the fault occurs. |
| Expected behavior | What should happen after those steps. |
| Actual behavior | What happens instead. Include the exact error text. |
| Completion checks | Observable results that prove the fault is fixed. Leave each checkbox unchecked. |
| Context | Related issues, logs, screenshots or a sample file. Separate observations from suspected causes. |

For command-line submissions, use these exact field labels as `###` headings. Keep their order. Write `Not provided` for an optional field you cannot fill. Use `gh issue create --repo glennawatson/HyperPDF --title '<title>' --body-file <file>`.

Give the smallest example that shows the fault. Record missing details as unknown. Use repo-relative paths for code references. Link an existing report when the fault is already tracked.
