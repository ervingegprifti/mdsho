# Contributing to MDSHO

MDSHO does not accept code contributions: pull requests and patches from outside the project are not merged. Bug
reports and suggestions are welcome in [Issues](https://github.com/ervingegprifti/mdsho/issues).

Keeping all the code from one author keeps the [dual licensing](COMMERCIAL-LICENSE.md) (GPLv3 and commercial) simple.
The GPLv3 still lets you fork MDSHO and change your own copy.

## Reporting a bug or suggesting a change

- Say what you did, what happened and what you expected, and which MDSHO version you use (shown after a panel's title,
  or in About).
- Please don't post code meant as a fix: fixes are written by the author, and code posted in an issue is not copied
  into MDSHO.

## Build and test your own copy

Requires the .NET SDK pinned in `global.json` and Windows (WPF).

```
dotnet build MDSHO.Cli.slnf -c Release
dotnet test MDSHO.Cli.slnf
```

`MDSHO.Cli.slnf` excludes the MSIX packaging project, which the dotnet CLI cannot build. Warnings are treated as errors.

## How MDSHO is developed

- **Tests first.** Write a failing test, see it fail, then implement until it passes. Bug fixes need a regression test.
- **Style.** Follow `.editorconfig`; keep changes focused. Source files use CRLF line endings (see `.gitattributes`).
- Every user-visible change gets a line in `CHANGELOG.md`, under the version in progress.
