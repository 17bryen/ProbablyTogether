# Building

Requirements: .NET SDK 9 (any SDK that can target netstandard2.1) and Probably Stolen installed.

    dotnet build -c Release

The project references the game's assemblies from `GameManaged`. It looks in the default Steam
folder (`C:\Program Files (x86)\Steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed`)
first. If the game is elsewhere, pass the folder:

    dotnet build -c Release -p:GameManaged="X:\...\Probably Stolen_Data\Managed"

- `-c Release` builds the mod with the test bots.
- `-c Ship` builds the player version, without the test bots. Releases are this build.

Output: `bin\<Release|Ship>\netstandard2.1\ProbablyTogether.dll` and `manifest.xml`. Install by
copying both into `<game>\Mods\ProbablyTogether\`. Close the game first: it locks the file.

Target: netstandard2.1, C# 12 syntax, unsafe allowed (Unity 2021.3 Mono runtime). Nullable off.
