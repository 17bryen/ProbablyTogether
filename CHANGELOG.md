# Changelog

All changes to Probably Together.

## 0.4.1 - 2026-11-10

Same protocol (5).

### Fixed
- "Start a new run" keeps the players' seats; everyone gets a new storage bay and picks a trade again.
- The calendar and the morning screen show the scaled rent that is actually charged.
- Joe Wild's menu opens on the caller's screen, not the host's, while the host is at Dr. Jackson's.

### Added
- Every new storage bay comes with a fanny pack; a bag in another player's bay cannot be taken on a trip.
- The host's Leave prompt offers "Step away (the shop stays open)", "Close the server" and "Keep playing".

## 0.4.0 - 2026-11-10

Protocol 5. Every player needs it. Older saves load.

### Added
- Players are remembered with a permanent seat, matched by Steam id; `maxPlayers` caps seats, not connections.
- Rent and customers scale with the shop's players, connected or not (`rentPercentPerPlayer`, `customersPerPlayer`; default +50% rent and +2 customers per extra player).
- Each player gets a storage bay only they can open.
- Each player picks one trade; its kit goes into their bay and its machines are theirs alone.
- The Host window has the two scaling settings and a switch for bays and trades (`playerOwnership`).

### Fixed
- The protocol number is now checked, so mismatched builds can no longer join each other.

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
