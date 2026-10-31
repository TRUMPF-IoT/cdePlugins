# 04-final-validation: Full solution validation and deferred recommendations

Perform a clean full rebuild of the solution and run any discoverable test projects. Confirm every project's output is produced under `net10.0` and the plugin packaging (`signandpackage.cmd`, `GeneratePackageOnBuild`) still emits artifacts. Document deferred recommendations: Central Package Management can now be added cleanly since all projects are SDK-style and on a single TFM (`net10.0`) without `VersionOverride` friction; note any packages intentionally left at non-latest versions and why. Create the single end-of-upgrade commit on `upgrade-dotnet-10` per the commit strategy.

**Done when**: Clean rebuild of the solution succeeds with 0 errors / 0 warnings; tests (if any) pass; deferred recommendations recorded; all changes committed in one commit on the working branch.
