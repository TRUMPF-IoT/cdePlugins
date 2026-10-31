# 01-verify-toolchain: Verify .NET 10 SDK and build toolchain prerequisites

Confirm the .NET 10 SDK is installed and usable from Visual Studio 2026 / `dotnet` CLI, check for any `global.json` that would pin an older SDK, and verify the solution restores and builds cleanly on its current netstandard targets so there is a known-good baseline before any changes. Also inventory the `BuildTools/signandpackage.cmd` post-build step used by every plugin project — it runs on every build and must keep working after the retarget (output paths change from `netstandard2.1` to `net10.0`).

Known risks: `GitVersion.MsBuild 5.6.6` is referenced by some projects and may need a newer version to work with the .NET 10 SDK; the legacy non-SDK projects `GnatMQ.WinRT`, `GnatMQ.NetCf39`, `GnatMQ.Net` (Net45) and `CDMyOPCUAClient.Tests` exist on disk — confirm whether they are part of the solution and therefore in scope.

**Done when**: .NET 10 SDK validated; no blocking `global.json`; baseline build result recorded; list of in-solution projects confirmed against assessment.

## Research Findings

### Toolchain
- `validate_dotnet_sdk_installation(net10.0)` → compatible SDK found. Installed SDKs: 10.0.100-rc.1, **10.0.403**, 11.0.100-rc.3 (`dotnet --version` = 11.0.100-rc.3, fine for net10.0).
- No `global.json` anywhere in the repo.
- `NuGet.config` (repo root): nuget.org only; `repositoryPath=./src/Nugets`.
- `Directory.Build.props` (repo root): `BaseOutputPath=$(SolutionDir)..\bin\$(Configuration)\$(MSBuildProjectName)\`, `OutputPath=$(BaseOutputPath)`; defines `CDE_STANDARD` only when TFM==netstandard2.0 (CDE_NET35/40/45 for legacy). Does NOT define TargetFramework. Adds `Microsoft.SourceLink.GitHub 1.0.0` outside VS.
- Build tool: `dotnet build` for all csproj (all SDK-style, no resx images/WPF/COM).

### In-solution projects (19, matches assessment)
18 csproj + `NModbus4Shared.shproj` (shared project imported via `.projitems` into CDMyModbus). Legacy `GnatMQ.WinRT/.NetCf39/.Net` and `CDMyOPCUAClient.Tests` are NOT in the solution → out of scope.
- TFM pattern: `<TargetFramework>netstandard2.1</TargetFramework>` in most; C-DMyComputer uses plural `<TargetFrameworks>netstandard2.1</TargetFrameworks>`; GnatMQ.Std targets netstandard2.0 (check in task 02).
- Common csproj pattern: `GeneratePackageOnBuild`, `ProjectName=$(AssemblyName).$(TargetFramework)`, `Product ... for $(TargetFramework)`, `PackageReference Update GitVersion.MsBuild 6.5.0`, floating `C-DEngine 6.*-*` and `cde*Shared 6.*-*` (IncludeAssets=ContentFiles), custom `<Target Name="PostBuild" AfterTargets="PostBuildEvent">` with `Exec signandpackage.cmd`.

### Post-build sign & package
`BuildTools/SignAndPackage.cmd` → `signMeSha.bat` (AzureSignTool if `real.sign` exists) → `cdePackager.exe <dll> <targetdir>`. Locally `BuildTools/real.sign` exists (gitignored, `.gitignore:229`) but no `CDE_CODE_SIGN` secrets → AzureSignTool error -1610612734 → MSB3077 in every project. `/p:PostBuildEvent=` does NOT bypass (custom target). Workaround used: renamed `BuildTools/real.sign` → `real.sign.disabled-for-upgrade` (gitignored path, no repo change). Restore after upgrade is complete.

### Baseline build (netstandard, signing disabled)
- **FAILED**: 6× CS0246 in CDMyModbus (`TheDeviceTagMapping`, `TheDeviceDescription` missing from C-DEngine 6.112.2). These types exist in **C-DEngine 7.110.1** (`lib/net10.0` only, nuget.org latest). → task 02 must move `C-DEngine` to `7.*`.
- Warnings: CS0436 ×85 unique (C-DMyVThings — contentFiles sources from cde*Shared packages conflict with the packages' compiled assemblies; `IncludeAssets=ContentFiles` missing/ineffective there), CS0618 ×56 unique (CDMyMSSQLStorage — `System.Data.SqlClient` obsolete → migrate to `Microsoft.Data.SqlClient`).
- Resolved cde* packages: cde*Shared 6.104.1 (latest on nuget.org), cdeOpc.Ua.* 1.0.0.
- Baseline log: `%TEMP%\baseline.log`.
