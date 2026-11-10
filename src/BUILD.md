# Building

Requirements: .NET SDK 9 (any SDK that can target netstandard2.1), Probably Stolen installed.

    dotnet build -c Release

The csproj references the game's assemblies by HintPath from `GameManaged`, which defaults to
`D:\Games Library\Steam Library\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed`.
On another PC: `dotnet build -c Release -p:GameManaged="X:\...\Probably Stolen_Data\Managed"`.

Output: `bin\Release\netstandard2.1\ProbablyTogether.dll`. Install by copying it over
`<game>\Mods\ProbablyTogether\ProbablyTogether.dll` (keep the manifest.xml beside it). The game must be
closed (server and client both lock the file).

Target: netstandard2.1, C# 12 syntax, unsafe allowed (Unity 2021.3 Mono runtime). Nullable off.
