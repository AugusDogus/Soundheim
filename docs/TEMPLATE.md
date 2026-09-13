# Start a new mod

This repository is a .NET template with the same layout and release pipeline as
Buildheim, ComfortView, and ShieldMeBruhReforged.

Install it from a local clone and generate a separate project:

```sh
dotnet new install /path/to/SoundheimTemplate
dotnet new valheim-mod -n MyMod -o /path/to/MyMod
```

The generator renames the project, solution, assembly, namespace, plugin ID,
package name, and local documentation references. Use an identifier containing
letters, digits, and underscores, beginning with a letter. The template includes
no Git history, downloaded game files, build outputs, or publishing credentials.

Before the first release:

1. Set the plugin ID and display name in `src/MyMod/Plugin.cs`.
2. Set the repository URL, description, and dependencies in `package/manifest.json`.
3. Replace `package/icon.png` with a 256 by 256 PNG and update the README and changelog.
4. Choose and add the appropriate project license. Do not inherit a fork's license accidentally.
5. Build, extend `scripts/check.sh` with actual mod checks, and test in-game.
6. Initialize a separate Git repository and push it to its own private GitHub repo.
7. Set `THUNDERSTORE_NAMESPACE` and `TCLI_AUTH_TOKEN` in that repo's Actions settings.
8. Follow [Release](DEVELOPMENT.md#release) when ready to publish.

Keep the shared build files and scripts aligned with the existing mods. The
workflow works from the generated repository without reaching into the template
repository. Only version tag pushes publish packages.
