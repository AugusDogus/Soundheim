# Soundheim

A client-side Valheim mod built with BepInEx 5.

## Installation

Install BepInExPack_Valheim 5.4.2350 in your mod profile. Copy `Soundheim.dll`
from a release ZIP into `BepInEx/plugins/Soundheim/`, then start the game modded.

## Build

```sh
bun install --frozen-lockfile
dotnet build src/Soundheim/Soundheim.csproj -c Release -t:Package \
  -p:GameDir="/path/to/Valheim" \
  -p:BepInExDir="/path/to/profile/BepInEx"
```

Requires .NET SDK 8 and Bun 1.4.1+. Output: `artifacts/Soundheim-1.0.0.zip`.

[Development and releases](docs/DEVELOPMENT.md) · [Repository layout](docs/REPOSITORY.md) · [Using the template](docs/TEMPLATE.md)
