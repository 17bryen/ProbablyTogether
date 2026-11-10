# Changelog

All changes to Probably Together.

## 0.3.0 - 2026-11-10

Protocol 3. Not compatible with the upstream mod's clients or servers.

### Added
- Individual night outings (server setting, off by default): each player's trip is private and reconciled on return.
- The Host window can pick a saved shop or start a new one with scenario, difficulty, skip intro and endless.
- Instant counter takeover (`counterTakeoverSeconds`, default 1).
- A warning when the upstream mod is also turned on.
- The host can step out without closing the shop, then rejoin or stop hosting.
- A step-away key (C, rebindable) hands the counter over.
- "Host a shop" on the main menu: this game becomes the server and the host plays in it.
- Other players see a dragged item as a ghost under the dragger's hand.
- Host mode (`--br-host`) runs the server and the host's game in one process.
- Launchers for hosting and test joins.

### Fixed
- The per-player customer bonus counts the host as running the current version.
- The day can end and the map closes again after game update 1.0.0.12.

### Removed
- The public server list, the server announce, the rent buttons and upstream branding.

### Changed
- Rebuilt from the upstream mod 0.2.8 as a buildable source tree.
