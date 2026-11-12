# Changelog

All changes to Probably Together.

## 0.7.2 - 2026-11-12

Same protocol (14). Every player should take it.

### Fixed
- Pressing a button on a chemistry machine with a module in it no longer makes copies appear to differ.
- The host's log always says what differs when a player's machine differs.

## 0.7.1 - 2026-11-12

Protocol 14. Every player needs it.

### Added
- Some customers know a player from that player's standing with their faction: they mention them, ask for them, or make an offer only that player can close.
- The reputation panel shows how much of each faction's standing each player earned.
- Deals made on a trip to Dr. Jackson's count for the traveller in the ledger.

### Changed
- The "X is at the counter" label is gone; the counter now only says when a shotgun is aimed or a customer will deal with one player only.

## 0.7.0 - 2026-11-12

Protocol 13. Every player needs it.

### Added
- The founder can use `/retrade <player>` to let a player give up their trade and pick again, and `/remove <player>` to remove an offline player for good. Only while the shop is closed; the trade's machines, kit, Joe Wild entries, permits and cartel ties go with it.
- A kit remembers which items it was, so a trade that leaves takes exactly those.
- A ledger tracks each player's cash in and out, goods sold and bought, and faction standing earned; `/ledger` prints it. A deal counts for the player who put the item on the counter.

## 0.6.2 - 2026-11-12

Protocol 12. Every player needs it.

### Fixed
- The Commissary's credit and stock are now shared: what one player spends on a trip is gone for the next.
- "Redeem all" on a trip now redeems what the traveller carries, and the server redeems what lies in the shop for everybody.

## 0.6.1 - 2026-11-12

Same protocol (11). Only the host needs it.

### Fixed
- A phone call left open when the night starts no longer makes players reload in a loop the next morning. The server hangs up open calls at nightfall and ends calls nobody holds.
- The host log no longer warns about "unexpected sys.ping" every ten seconds.

## 0.6.0 - 2026-11-12

Protocol 11. Every player needs it.

### Added
- In shops where everyone goes out alone, each player has their own wound, treated, healed and reported separately. Older shops start everyone from the shop's one wound.
- Every replayed action records which player did it.

### Changed
- A customer who asks for a player now says so in separate lines, paced like the game's conversations.

## 0.5.2 - 2026-11-12

Same protocol (10).

### Fixed
- Joe Wild's window now opens for a player at home while the host is at Dr. Jackson's.
- A travelling player's game no longer runs the shop's chemistry by itself.
- A travelling player's bag copy no longer logs a warning at every trip start.

### Added
- Desync reports now show both sides' conversations and each machine's fingerprint.

## 0.5.1 - 2026-11-11

Protocol 10. Every player needs it.

### Added
- A trade bought through Joe Wild brings its starting kit into the buyer's storage bay.
- A trade bought through Joe Wild has no scripted visitors; its weekly supplier still comes.
- Buying a new trade is refused while anyone is out on a trip.

### Changed
- Kit items that find no room anywhere are turned into cash, with a notice, instead of being lost.

## 0.5.0 - 2026-11-11

Protocol 9. Every player needs it.

### Added
- Joe Wild's trade entries are personal: a held trade's entries can only be bought by its holder, and buying an unheld trade's entry makes the buyer its holder. The list says who holds what.
- The rest of Joe Wild's list stays the shop's.
- A trade's weekly supplier comes for its holder and greets them.

### Fixed
- Weekly trade suppliers are now really tied to a player's trade.

## 0.4.9 - 2026-11-11

Protocol 8. Every player needs it.

### Changed
- Coming home from a trip no longer reloads the shop: the shop stays live underneath while a player is out.
- A player's deals at Dr. Jackson's now count for the shop.
- The shop's cash updates live on a traveller's screen.
- Customers who come for a player's trade now say so in their own words, replacing the on-screen notice.

## 0.4.8 - 2026-11-11

Protocol 7. Every player needs it.

### Added
- When a copy disagrees with the server, both sides are saved to a report in the shop's `desync-reports` folder and the log names what differed.
- Differences a copy sits out (at night, during trips) are reported too, at most once a minute.

### Fixed
- The host's spending on a trip now reaches the players as the full amount.
- The replay check no longer asks for a reload at night or while someone is out.

## 0.4.7 - 2026-11-11

Protocol 6. Every player needs it.

### Added
- A trade's scripted customers come for the player who holds it, on a calendar counted from when they picked it (the chemist's cartel, the fence's contact, the gunsmith's first buyers, the junker's introduction).
- Weekly trade suppliers come when any player holds that trade.
- A customer who came for a trade is announced with the player's name at the counter.

## 0.4.6 - 2026-11-11

Same protocol (5).

### Fixed
- Opening the Clientele panel before opening no longer makes customers differ from the start of the day.

## 0.4.5 - 2026-11-11

Same protocol (5).

### Fixed
- The first player to pick a trade no longer closes the pick window for everyone.
- A player is asked for a trade again right after rejoining or a reload.
- A host who picks through the window has it closed once the pick is in.

## 0.4.4 - 2026-11-11

Same protocol (5).

### Fixed
- In a hosted shop, chemistry (heating, cooling, reactions, the fridge) now runs all the time, not only while the host is out.
- A hosted shop now sends temperatures and liquid contents to players, every second.
- Hopper runs scheduled by the server are no longer dropped on a host.

## 0.4.3 - 2026-11-11

Same protocol (5).

### Fixed
- The host's sales at Dr. Jackson's no longer cause reload storms. Their effects are sent when the host is home, and copies no longer reload by themselves at night or while someone is out.
- Coming home at night after a reload no longer shows the morning report and an open shutter.
- Players at the dump on the same night no longer find the same items.

## 0.4.2 - 2026-11-11

Same protocol (5).

### Fixed
- Another player's drop no longer knocks the item out of your hand.

### Added
- An item another player is holding cannot be picked up until they let go.

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
