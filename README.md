# Crystal Code GitHub

**A Crystal Code integration for GitHub pull requests and issues.**

This repository is the empty skeleton. It does not review pull requests, review issues, or classify issues yet.

[Build from source](#build-from-source)

## Build from source

You need the .NET 10 SDK.

```bash
dotnet build CrystalCode.Integrations.GitHub.sln
dotnet test CrystalCode.Integrations.GitHub.sln
```

The production project has no NuGet packages and no product types yet. The test project references it and uses xUnit and Microsoft.NET.Test.Sdk only.
