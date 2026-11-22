# ADR-083: A seat for every rival god

- Status: Accepted
- Date: 2026-11-22
- Amends: [ADR-018](ADR-018-multiplayer-host-snapshots-and-intents.md) (at most three players join) and
  [ADR-082](ADR-082-the-new-world-choices.md) (a world of up to eight gods)

## Context

ADR-018 let at most three players join a host, one for each AI god of the four-god world of the time
(`NetworkProtocol.MaxClients`). ADR-082 lets a world hold up to eight gods, and the README promised a
seat for each. The owner asked for a shared world to be tried. It was, with eight game processes on
one Mac, joining over its network address:

- **Six players at once:** each replica matched the host.
- **The fourth friend:** in an eight-god world it could only join because six were tried.
- **The cap:** reading the code showed ENet's server was made with room for three.
- **An eighth player at a full world:** ENet dropped the connection before the game could answer, so
  the player read "the host did not answer" instead of why.

## Decision

- **Seats:** `NetworkProtocol.MaxClients` is 7, one for every rival of an eight-god world, as the
  specification's "1–8 gods" asks. A world of fewer gods has fewer seats: the host refuses a player
  when no god is free, as before.
- **A spare connection:** the host's ENet accepts `NetworkProtocol.MaxConnections`, one more than the
  seats. A player arriving at a full world connects, is refused with "No god is free", and is let go,
  which frees the spare.
- **F1 counts the world's seats:** "1 of 3 players joined" in a four-god world, "of 7" in an
  eight-god one.
- **The LAN answer** already counts the gods still free (ADR-081). It is now capped by seven rather
  than three.

## Consequences

- **Eight players** can share a world. `MultiplayerSessionTests` seats seven in an eight-god world
  and refuses an eighth.
- **Live:** seven players seated at once, and the eighth read "No god is free".
- **Load:** a host sends each player its sections, so seven players cost it more upload than three.
  The six-player run received about 1.6 KB/s each in its first seconds. Nothing heavier was measured
  on one Mac.
- **Not tried:** two physical computers; every process ran on one Mac, over its network address.
  Seven players loading a large world at the same moment on that one Mac each stalled for about 9 s,
  past ENet's timeout, and were dropped. Joined one after another they stayed. Separate computers
  do not share that load.
