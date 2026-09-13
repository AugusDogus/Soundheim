# Live Valheim development

`ValheimDevBridge` is a local development plugin, excluded from the Thunderstore
package. It listens only on `127.0.0.1:19287`, requires a random access key, and
rejects browser-origin requests. The key stays in the local profile at
`BepInEx/config/ValheimDevBridge.key`; never commit it.

After installing the bridge and the reload-enabled audio mod, restart Valheim
once. Keep using that game process while editing:

```sh
bun dev/bridge.mjs status
bun dev/bridge.mjs logs 'Custom Audio'
bun dev/bridge.mjs ui
bun dev/bridge.mjs screenshot /tmp/valheim-dev.png
# Rebuild src/Soundheim, then load the new DLL into the running game:
bun dev/bridge.mjs reload
```

The CLI defaults to r2modman's Default profile. Set `VALHEIM_PROFILE` for another
profile. The bridge's `Assembly path` setting in `augusdogus.dev.bridge.cfg`
points to the local build DLL. Remote requests cannot choose arbitrary DLL paths.

UI inspection, screenshots, and plugin lifecycle work run on Unity's main
thread. Reload waits for active audio commands to finish, removes the old plugin,
UI and Harmony patches, and loads the rebuilt assembly. Open settings remain
available. Unsaved output previews revert to the saved setting during reload.

Mono retains old assemblies until the game exits, so restart after many reloads
or when changing dependencies. The bridge itself requires a restart to update.
Disable the bridge in r2modman when finished developing.

Build the development plugin separately with your usual game references:

```sh
dotnet build dev/ValheimDevBridge/ValheimDevBridge.csproj -c Release \
  -p:BepInExDir="/path/to/profile/BepInEx"
```

The public mod package contains only `Soundheim.dll`, never this bridge.
