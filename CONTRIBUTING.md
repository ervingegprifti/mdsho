# Contributing to MDSHO

Thanks for your interest. Bug reports and pull requests are welcome.

## Build and test

Requires the .NET SDK pinned in `global.json` and Windows (WPF).

```
dotnet build MDSHO.Cli.slnf -c Release
dotnet test MDSHO.Cli.slnf
```

`MDSHO.Cli.slnf` excludes the MSIX packaging project, which the dotnet CLI cannot build. Warnings are treated as errors.

## Expectations

- **Tests first.** Write a failing test, see it fail, then implement until it passes. Bug fixes need a regression test.
- **Style.** Follow `.editorconfig`; keep changes focused. Source files use CRLF line endings (see `.gitattributes`).
- Keep pull requests small and describe the user-visible change. Add a line to `CHANGELOG.md` under `[Unreleased]` when it affects users.

## Contributor grant

By submitting a contribution (pull request, patch or any other form) you agree that:

1. you license it under the [GPLv3](LICENSE), the same license as the project;
2. you also grant the author, Ervin Gegprifti, a perpetual, worldwide, irrevocable, royalty-free right to use, modify, sublicense and relicense your contribution under any terms, including commercial and proprietary licenses (this is what allows the [dual licensing](COMMERCIAL-LICENSE.md) of MDSHO);
3. you have the right to submit the contribution, and it does not include code you do not have the right to license in this way.

Your contribution is given without payment. You will not receive any payment, royalty or share of income from MDSHO, including from commercial licenses or a sale of the project, and the author may sell, license or transfer MDSHO without your consent.

If you cannot agree to this, please open an issue instead of a pull request.
