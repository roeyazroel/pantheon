# ADR-024: Culture: doctrines, secularism, dependency, resentment, missionaries, revolts and gods' traits

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-009](ADR-009-belief-and-divine-power.md) (belief and Divine Power),
  [ADR-021](ADR-021-domains-holy-sites-schisms-species-settings-and-audio.md) (domains and schisms)
  and [ADR-023](ADR-023-settlement-society-priorities-buildings-and-technology.md) (society)
- Amends: [ADR-019](ADR-019-replication-by-section-and-rate.md): the settlements section is sent
  every 2 s rather than every 1 s

## Context

The specification asks for more of belief than faith, love, fear and doubt, and for gods whose
reputation comes from what they do:

- **§7 and §8:** people come to depend on a god that helps them and expect its protection, and
  resentment builds where fear rules, ending in rebellion.
- **§11 and §3.2:** gods are not scored good or evil. Traits such as compassionate, cruel,
  vengeful, merciful, generous, deceptive, honest, chaotic and ambitious emerge from their acts.
- **§33:** cultures form doctrines (pacifism, militarism, sacrifice, charity, nature worship,
  knowledge seeking, expansionism, isolationism) that change how they act and believe.
- **§34:** a god acting against its reputation shocks its followers.
- **§35:** settlements can lose belief: agnostic, atheist, then anti-divine, with less power,
  resistance to miracles, science bonuses and anti-divine movements that spread.
- **§32:** temples send missionaries.

## Decision

### State

- **`SettlementState.Culture` (`CultureState`):** how firmly a settlement holds each of the eight
  doctrines (0..1) and its `Secularism` (0..1). A secular stage is derived from secularism by the
  thresholds `Agnostic`, `Atheist` and `AntiDivine`; it is never stored.
- **`GodBelief.Dependency` and `GodBelief.Resentment` (0..1):** §7–8's deeper feelings, alongside
  faith, love, fear and doubt. Migration groups carry them, and new villages inherit them.
- **`GodState.Conduct` (`GodConduct`):** what the world remembers of a god's acts, as weights that
  fade (`ConductFadePerSecond`): help, gifts, harm, cruelty (harm to its own followers),
  vengeance (harm to the followers of a god that wronged it), upheaval (fires, quakes, floods),
  deceit, faithfulness and aggression.
- **`GodState.Traits`:** what the world calls the god, derived from its conduct each culture step
  against `TraitThreshold`. For example compassionate needs help of at least the threshold and at
  least twice the harm; cruel needs cruelty of half the threshold, or harm well above help.
- **Rules:** `DivineRules.Culture` (`CultureRules`) in `divine_rules.json`.

### Where acts are noted

- **`MiracleCaster`:** through `WitnessCulture.NoteCast`, once a miracle is seen.
  - Kind miracles count as help where they met a need, and as gifts otherwise.
  - Harmful ones count as harm (more with each death). They also count as cruelty over the god's
    own followers, vengeance over a guilty god's, and upheaval when they ravage the land.
- **`DiplomacyAuthority`:** a declared war is aggression; peace and alliances are faithfulness
  on both sides; a broken alliance is deceit.

### What witnesses take from a miracle

`WitnessCulture.Witnessed` runs after the existing impulse:

- **Harm to the god's own followers:** it breeds resentment, more among pacifists and, when the
  land is ravaged, among nature worshippers, and less among militarists. Among the fearful it
  nudges sacrifice.
- **Help in need:** it breeds dependency and nudges charity. Blessings of the land nudge nature
  worship.
- **Betrayed love:** the doubt is multiplied by `1 + Contradiction` when the god is known as
  compassionate or merciful (§34).
- **How much a miracle moves its witnesses:** secularism lessens it (`MiracleResistance`).
  Nature worship strengthens blessings of the land, and charity deepens gratitude.

### The culture step

`CultureSystem` runs after the society step, at the belief step's interval.

- **Doctrines:** each drifts towards a target (`DoctrineDriftPerSecond`) from what the settlement
  lives through, the god most of it follows, that god's domains and traits (never its
  personality), and the settlement's priority. `DoctrineEmbraced` is raised when a doctrine
  passes `Embraced`.

  | Doctrine | Target from | Effect, through `DoctrineEffect` |
  | --- | --- | --- |
  | Pacifism | peace × safety; a merciful or compassionate god | harm by its god outrages more |
  | Militarism | war; a Defence priority; a War domain or ambitious god | harm outrages less |
  | Sacrifice | fear; a Death domain | fear worships harder (`SacrificeWorship`) and breeds less resentment |
  | Charity | love; a compassionate or generous god | gratitude deepens; full stores feed starving neighbours (`CharityPerSecond`) |
  | Nature worship | blessed farmland; a Nature domain | blessings move more; ravaging outrages more |
  | Knowledge seeking | education; a Knowledge domain | learning is faster; secularism tends a little higher |
  | Expansionism | an Expansion priority; crowding; an ambitious god | fewer people are needed to found a village |
  | Isolationism | few neighbours; a god that turns refugees away; war | other gods pull less; migrants are drawn less |

- **Secularism:** it drifts towards a target (`SecularDriftPerSecond`). Doubt, the godless share,
  an Old Faith, resentment and science raise the target, weighted by `Secular`; faith lowers it.
  `SecularismRose` is raised at each change of stage.
  - Secularism lowers the power raised (`SecularPowerLoss`), every god's pull
    (`SecularResistance`) and what miracles do. It speeds learning (`SecularScience`).
  - An anti-divine settlement loses `AntiDivineApostasy` of every god's followers each step, and
    raises its neighbours' secularism (`SecularSpreadPerSecond`).
- **Dependency and resentment:** each step, fear breeds resentment (`ResentmentFromFear`, softened
  by sacrifice), love dissolves it (`ResentmentFromLove`), and dependency fades (`DependencyFade`).
  - Dependency raises the power a belief yields (`DependencyWorship`).
  - In hardship the dependent doubt more (`DependencyExpectation`): §8's expectation of
    protection.
- **Missionaries (§32):** each temple wins people who follow no god in the settlements within
  `MissionRange`. The rate is `MissionPerSecond`, scaled by the zeal of the temple's followers and
  the target's openness.
- **Revolts:** in an unsettled settlement (stability below 0.5), when a god followed by at least
  30% is resented past `RebellionResentment` and feared, these things happen and `Rebellion` is
  raised:
  - `RebellionShare` of its followers renounce it;
  - its temple is torn down and its priority dropped;
  - the resentment is vented (`RebellionVent`), and stability falls.
- **Traits:** conduct fades. Traits are recomputed, and `TraitEarned` is raised for each new one.

### Saves, multiplayer and presentation

- **Saves:** version 16. Migrating from 15 gives every settlement a culture with no doctrines and
  no secularism, every belief no dependency or resentment, and every god no conduct or traits.
  `CultureIntegrity` checks the ranges and that each trait appears only once.
- **Multiplayer:** the protocol is unchanged (version 4). Culture travels with the settlements and
  gods. The larger settlements section is sent every 2 s rather than ADR-019's 1 s, which keeps
  four players within the traffic budget. Villages change slowly, and urgent changes still go out
  with the events.
- **Alerts:** they tell of:
  - revolts against the player, or in its villages;
  - its villages turning atheist or anti-divine;
  - doctrines its villages embrace;
  - traits earned by any god.
- **History:** "The Revolt of … against …", "… renounces the gods" (anti-divine) and "…
  embraces …" (villages of at least `ConversionPopulation`, once per doctrine within the repeat
  time).
- **Seeing culture:**
  - The Settlements panel shows doctrines, the secular stage and each god's love, fear, need and
    resentment.
  - The HUD's god line and the diplomacy panel show traits.
  - The Culture overlay colours each settlement's lands by its strongest doctrine, or grey when
    it is at least agnostic.
  - F1 lists each settlement's culture.
  - `--doctrine=<doctrine>|secular` shapes the focused village.

## Consequences

- **Reputation:** gods earn reputations from what they do. In ten passive-player games, the gods
  of peace and love become compassionate and generous, and those of fear cruel, vengeful and
  chaotic.
- **Effects:** doctrines and secularism change power, conversion and miracles by factors applied
  inside the existing steps, so the systems stay independent.
- **Balance:** measured over 40 seeds against part 3's build (see the plan).
- **Revolts:** they need a god both feared and resented in an unsettled village, so they are rare
  in games among the AI. A cruel player meets them.
- **What later parts build on:**
  - Politics (part 5) can read doctrines and traits: militarist villages and ambitious gods.
  - The cultural victory (part 6) can read doctrines.
- **Not modelled:**
  - Missionaries as walking people; they are a rate.
  - Doctrines invented or named by players.
