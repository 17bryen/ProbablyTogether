# Changelog

All changes to Probably Together.

## 0.3.2 - 2026-11-10

Same protocol (4).

### Changed
- Clearing the counter at home while the host is out now works, including while the host is at Dr. Jackson's.

### Added
- Test tools for setting up a shop's state and starting tests from any save.

## 0.3.1 - 2026-11-10

Protocol 4. Every player needs it.

### Added
- The host can go out alone too when outings are individual, while players at home keep playing the shop.
- At Dr. Jackson's, players at home keep working on copies of the counters while the host uses the real ones.
- Each player in the shop at closing time gets one night trip when outings are individual.

### Changed
- With individual outings on, there is no group trip any more.
- The host cannot step out while on a night trip.

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
