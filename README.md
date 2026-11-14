# MDSHO

Multi Desktop Shortcut Organizer: a WPF app (.NET 10, x64) for organizing desktop shortcuts across multiple desktops/screens.

<a href="https://apps.microsoft.com/detail/9P8DNCD0PPG8"><img src="https://get.microsoft.com/images/en-us%20dark.svg" alt="Get it from Microsoft" width="200"></a>

## Documentation

The documentation is in [`docs`](docs/index.md) and is published at <https://ervingegprifti.github.io/mdsho/>:

- [Privacy Policy](docs/privacy.md) and [Terms and Conditions](docs/terms.md)
- [Architecture](docs/architecture.md): projects and folders, dependencies, where the data lives, export/import, database upgrades
- [Database](docs/database.md): tables, columns, indexes and upgrade history
- [Building MDSHO](docs/building.md): build, test, run, and make a signed sideload MSIX

## Build and test

```
dotnet build MDSHO.Cli.slnf -c Release
dotnet test MDSHO.Cli.slnf -c Release
```

## MSIX

MDSHO ships only as MSIX (Microsoft Store and sideload). The `.wapproj` can't be built with the dotnet CLI: open `MDSHO.slnx` in Visual Studio with the MSIX packaging tools installed and use *Publish > Create App Packages* on `MDSHO.Package`. See [Building MDSHO](docs/building.md) for a signed sideload package.

## License

MDSHO is open source under the [GNU General Public License v3.0](LICENSE): you can read, change and share the code, as long as what you share stays open source under the same license. A [commercial license](COMMERCIAL-LICENSE.md) is available for uses the GPLv3 does not allow, such as closed-source redistribution.

The name "MDSHO", its logo and the MDSHOFont icon font are not covered by the GPLv3; see [TRADEMARKS.md](TRADEMARKS.md). Third-party components are listed in [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Contributing

MDSHO does not accept code contributions; bug reports and suggestions are welcome in [Issues](https://github.com/ervingegprifti/mdsho/issues). See [CONTRIBUTING.md](CONTRIBUTING.md), which also shows how to build and test your own copy.
