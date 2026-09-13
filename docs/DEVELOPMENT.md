# Development

The public mod name is **Custom Audio Output Device** and the Thunderstore
package identifier is `Custom_Audio_Output_Device`. The repository, project,
DLL, and existing configuration identity retain the internal name `Soundheim`.

## Build

Install .NET SDK 8 and Bun 1.4.1 or newer. Run `bun install --frozen-lockfile`
from the repository root to install the release tooling. Use your Valheim
installation and BepInEx 5 profile:

```sh
dotnet build src/Soundheim/Soundheim.csproj -c Release \
  -p:GameDir="/path/to/Valheim" \
  -p:BepInExDir="/path/to/profile/BepInEx"
```

Output: `src/Soundheim/bin/Release/netstandard2.1/Soundheim.dll`.

`GameDir` defaults to a standard Steam installation on Linux or Windows.
`BepInExDir` defaults to `GameDir/BepInEx`; set it explicitly for r2modman.
Override `ManagedDir` for a dedicated server or another game data directory.
`Environment.props` is ignored by Git and may store local MSBuild properties:

```xml
<Project>
  <PropertyGroup>
    <GameDir>/path/to/Valheim</GameDir>
    <BepInExDir>/path/to/profile/BepInEx</BepInExDir>
  </PropertyGroup>
</Project>
```

## Check

After building, run the mod's checks with the same references:

```sh
MANAGED_DIR="/path/to/Valheim/valheim_Data/Managed" \
BEPINEX_DIR="/path/to/profile/BepInEx" bash scripts/check.sh
bun run typecheck
bun test tests/
```

The TypeScript package checks and JavaScript version checks use Bun's test runner.

The C# tests cover device discovery, exact process-ID filtering, default-device
selection, reconnects, failures, and the private game fields used by the settings hooks.
They do not render Unity UI. Check the Audio tab's layout, dropdown navigation,
preview, OK, Back, and reopening settings in game before releasing.

To run the optional Linux integration test, prefix the check command with
`SOUNDHEIM_LIVE_AUDIO_TEST=1`. It requires `pactl` and `pacat`, creates a temporary
null sink and two silent test streams, checks that only the selected stream moves,
then removes them. It does not route the real game's audio or change the system default.

The `ListsHostOutputsFromTheSteamRuntime` live test also runs inside Steam's
Soldier runtime to verify audio discovery when the container has no `pactl`.
In that case, the mod calls `steam-runtime-launch-client --alongside-steam`
to run the host command. Arguments stay separate, without a command shell.

Windows support is experimental and has not been run on Windows. It uses an
undocumented per-app audio policy interface, with IDs and ABI layout referenced
from EarTrumpet. Check Windows 10 and 11 with speakers, USB/Bluetooth devices,
unplug/reconnect, System default, and OK/Back before changing this support label.
Native handles and COM apartments are released on the worker thread. The code
never calls a system-wide default-endpoint setter.

## Package

Add `-t:Package` to the Release build command. The TypeScript script creates and validates
`artifacts/<PackageName>-<Version>.zip`. Import this ZIP with r2modman's
**Import local mod**. Build and package commands do not install or publish anything.

`package/manifest.json` defines the Thunderstore identity and dependencies.
Only the plugin DLL, package assets, README, changelog, and available license
notices enter the ZIP. Game, BepInEx, and NuGet dependency DLLs are excluded.
README image links use raw GitHub URLs pinned to the package version tag.
The repository and that tag must be public for Thunderstore to display images;
bundling images in the ZIP alone does not host them on the mod page.

## GitHub Actions

The identical **Build and publish** workflow in each mod builds on pushes to
`main`, pull requests, manual runs, and `vMAJOR.MINOR.PATCH` tags. It downloads
current public Valheim dedicated-server assemblies using anonymous SteamCMD
(app 896660), and the BepInEx version from `package/manifest.json`.
No Steam credentials or local game files are needed.

The workflow runs `scripts/check.sh`, package tests, and version tests, then
uploads the validated ZIP as `thunderstore-package`. Tag pushes additionally
create a GitHub release and publish that same ZIP with [Thunderstore CLI](https://github.com/thunderstore-io/thunderstore-cli).
The publishing token is passed only to the publish step. Branch pushes, pull requests,
and manual runs without a release tag only build and check.

Repository Actions settings:

- Variable `THUNDERSTORE_NAMESPACE`: `AugusDogus`.
- Secret `TCLI_AUTH_TOKEN`: a service-account access token for that Thunderstore team.

## Release

Keep the existing version until a release is ready. Update `CHANGELOG.md` and
commit your changes first. From a clean checkout, use Node.js 22.18+ or 24.11+:

```sh
npx bumpp@12.3.0 --release patch
# Inspect the generated commit and substitute its tag below.
git push --atomic origin HEAD:main vMAJOR.MINOR.PATCH
```

Use `--release 1.0.0` to tag an unreleased `1.0.0` without incrementing it, or
supply another explicit version. The shared bump config updates the manifest,
project version, plugin constant, and assembly versions together. Existing tags
are never overwritten. Check `git remote -v` before pushing from an old checkout
that also has an upstream remote.

Tags must match the source versions exactly. Prerelease tags are not published.
Creating a release manually on GitHub does not trigger publishing. To retry an
existing version with the current workflow, run **Build and publish** from `main`
and set `release_tag` to its existing tag (for example, `v1.0.0`). This checks out
and validates that tag before publishing; the tag is never moved.
If Thunderstore publishing fails, the ZIP remains on the GitHub release.
Correct credentials or settings and rerun the failed job. If the version is
already published on Thunderstore, release a new version.
