# Changelog

All changes to Probably Together.

## 0.9.7 - 2026-11-19

Same protocol (17). Every player needs it.

### Fixed
- A trade picked by a joining player brings its starting reputation again, counted in that player's ledger row. Its starting cash and rent are not charged to the shop.

## 0.9.6 - 2026-11-19

Same protocol (17). Every player needs it.

### Fixed
- Players no longer keep "catching up with the shop" while Dr. Jackson is at the counter. His stall leaves out the Commissary items only if every player is banned.
- A shop whose founder is banned from the Commissary now gets the Stamp Guide and the Logo Checker once a player who is not banned joins.

## 0.9.5 - 2026-11-19

Same protocol (17). The host needs it; every player needs it for trade picks.

### Fixed
- A hosted shop now starts with its starting reputation, permits and unlocks. Shops already started keep what they have.
- Picking a trade no longer moves the shop's reputation.

## 0.9.4 - 2026-11-18

Same protocol (17). Only the host needs it.

### Fixed
- Short Sleeper: the night's report no longer claims a customer was lost when only the short sleeper went out.

## 0.9.3 - 2026-11-16

Same protocol (17).

### Fixed
- Going back on the host screens shows the previous screen at once, without a one-frame flash.
- The corner button of "Your shops" is now a back arrow, not a cross.

## 0.9.2 - 2026-11-16

Same protocol (17).

### Fixed
- Going back through the host screens no longer shows the bare menu for a moment.

## 0.9.1 - 2026-11-16

Same protocol (17).

### Changed
- The released file no longer contains the test bots and is about half the size.
- The developer console is no longer closed through its library by name.
- The mod's network loops stop when the game stops the mod.
- `<GameVersions>` lists both `0.46D` and `1.0.0`.
- "Your shops" lists save files moved into the hosted folder by hand.

## 0.9.0 - 2026-11-15

Same protocol (17). Follows the game's December 10, 2026 mod rules.

### Changed
- Hosted shops are now kept in a `ProbablyTogether` subfolder of the game's save folder, so the game without the mod never lists or loads them. They are moved there once, the first time "Host a shop" is opened.
- The mod no longer patches the save library, runs threads of its own, starts coroutines on game objects, or patches Unity's audio.

## 0.8.7 - 2026-11-15

Same protocol (17).

### Fixed
- The About window's paragraphs no longer overlap.

## 0.8.6 - 2026-11-15

Same protocol (17).

### Fixed
- Buttons in the new panels are the game's size, evenly spaced, centred, and ordered from least to most drastic action.

## 0.8.5 - 2026-11-15

Same protocol (17).

### Changed
- Join a shop, Disconnected, Mods for this shop, the update notice, About and Leaving your shop now use the game's own panel. Join a shop takes an address pasted with its port.
- The upstream mod's name no longer appears in the game.

## 0.8.4 - 2026-11-15

Same protocol (17). Every player needs it for `/retrade` and `/reperk`.

### Changed
- `/retrade <player>` now offers the player the game's trade screen; nothing changes until they press Continue, and Back keeps everything.
- Joining, loading and starting a shop show a full-screen loading screen in the game's style instead of the mod's small window.

### Added
- `/reperk <player>` lets the founder let a player pick their perks again on the game's perk screen. Kept perks are untouched, dropped perks stop working, and new ones act as a first pick.

### Fixed
- The host now sees why their own perk pick was refused.
- The perk screen no longer opens empty for a player who has never finished a run.

## 0.8.3 - 2026-11-15

Same protocol (17). The host needs it; players should take it.

### Changed
- "Host a shop" shows the PC's local IP address (read-only) and a reminder to port-forward the port (TCP).

### Added
- A test suite that checks all 48 perks in a shop of two.

### Fixed
- Aug Menace: when its holder dies at night, every player now gets the next morning.
- A founder banned from the Commissary no longer closes it for the whole shop.
- Gambling Addiction's lottery unlock and other personal-perk checks now give the same answer on every copy.

## 0.8.2 - 2026-11-15

Same protocol (17). Only the host needs it.

### Changed
- Hosting runs in a new order, all in the game's style: host settings, then "Your shops", then "New shop" with the per-player rules, then the game's own new-game screens. Loading a shop hosts it at once, and Back goes one screen back.
- A shop keeps the rules it was made with; older shops use the rules last used.

## 0.8.1 - 2026-11-15

Same protocol (17).

### Fixed
- Layout fixes on the new host screens: overlapping and clipped text, and the "Start Game" button now reads "Continue".
- Continue on the trade screen looks disabled while waiting for the shop.

## 0.8.0 - 2026-11-15

Protocol 17. Every player needs it.

### Added
- Every player picks their own starting perks once. Each perk is unique to the shop, stacking per holder, or personal, as decided perk by perk; `/perks` lists who has what.
- A perk that would end the run ends its holder instead: they start over in their seat, or leave the shop if away.
- New players go through the game's own trade and perk screens; in older shops everybody picks perks at their next join.
- Hosting uses the game's own Load Game and New Game screens, with rename, duplicate, delete and earlier mornings to continue from.
- A shop whose run ended stays in the list and can be continued from its last or an earlier morning.

### Fixed
- The founder of a new shop no longer gets two fanny packs.
- The items every shop gets when skipping the intro now stay in the shop's storage instead of the founder's bay.
- A customer's own item can no longer be put into a player's storage bay.

## 0.7.6 - 2026-11-15

Protocol 16. Every player needs it.

### Changed
- A trade's visitors and weekly callers ask for its player by name again, with lines that fit the visit (news on the first day, "It's me again" on repeat visits).
- A random customer names a player only when the shop and that player have real standing with the customer's faction (10, 20 and 35 points for a remark, a request and a private offer).

## 0.7.5 - 2026-11-15

Protocol 15. Every player needs it.

### Changed
- A trade's visitors no longer ask for its player by name, except on the trade's first day.
- Customers who know a player are rarer: at most one a day for the shop, only buyers of goods that player has sold to their faction, and only after many such sales. Their private offer no longer gives a discount.
- Added lines are said once for certain and are written to the log.

## 0.7.4 - 2026-11-13

Same protocol (14). Every player needs it.

### Fixed
- Joining after someone moved items inside a storage that sits in their storage bay no longer causes endless reloads.
- A move the server made is applied even if the local game refuses it, instead of costing a reload.

### Changed
- When an item cannot be moved, the log now says which of the game's rules refused it.

## 0.7.3 - 2026-11-13

Same protocol (14). Every player needs it.

### Fixed
- Joe Wild's counter deliveries now land on the shop's counter, not Dr. Jackson's, while a player is at Dr. Jackson's.

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
