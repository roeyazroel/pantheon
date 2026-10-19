# ADR-042: Religion's look on villages

- Status: Accepted
- Date: 2026-10-19
- Builds on: [ADR-008](ADR-008-settlement-step-and-representative-citizens.md) (presentation
  derived from the world), [ADR-023](ADR-023-settlement-society-priorities-buildings-and-technology.md)
  (temples and walls) and [ADR-024](ADR-024-doctrines-secularism-dependency-resentment-and-traits.md) (doctrines
  and gods' traits)

## Context

§61 asks that religion visibly affect civilizations:

- a benevolent nature god might produce gardens, organic structures and wildlife;
- a fearful war god might produce intimidating monuments, fortifications and darker ceremonies.

Villages looked the same whoever they followed, but for the god's colour on their label and
temple, and the walls a village builds in war.

## Decision

### The style (`ReligiousStyles.Of`, Game layer, pure)

Each village takes a motif from the god most of its people follow. With no god holding most of it,
no style shows. Each motif is scored by four things, weighted from `settlement_view.json`
(`Style`):

| Motif | Domains | Traits | Doctrines | Feeling |
| --- | --- | --- | --- | --- |
| Gardens | Nature, Life, Prosperity, Ocean | Compassionate, merciful, generous | Nature worship, pacifism, charity | Love |
| Monuments | War, Death, Fire, Storm, Chaos | Cruel, vengeful, ambitious, chaotic | Militarism, sacrifice, expansionism | Fear |
| Lore | Knowledge, Justice, Protection, Trickery | Honest, deceptive | Knowledge seeking, isolationism | Half the faith |

| Factor | Weight |
| --- | --- |
| The share of the god's domains in the motif | 0.4 |
| The strongest doctrine of the motif | 0.3 |
| A trait of the motif | 0.15 |
| The feeling | 0.15 |

The best motif shows with its score times the god's share of the village, from 0.12.

### The pieces (`ReligiousStyles.Layout`, pure)

- **How many:** up to 14 at full strength in a village of a dozen houses or more.
- **Gardens:** flower beds in five colours and green bushes, on a spiral among the houses, clear of
  houses, the temple and water.
- **Monuments:** dark obelisks and burning braziers by turns, in a ring 5 m about the shrine.
- **Lore:** white columns in a ring 5 m about the shrine, at least five.
- **Placement:** rings spread evenly over the arc the temple's side leaves free. Pieces are placed
  from the seed and ids (`PresentationHash`), never saved, so showing them changes nothing.

### Drawing them

- **`StyleView`:** the client draws them with one MultiMesh per kind under the settlement's node,
  flowers and bushes tinted per instance and braziers crowned with an unshaded glowing flame.
- **Rebuilding:** only when the motif, the god, the number of pieces or the village's reach
  changes.
- **F1:** the selected settlement's style, its strength and its reasons.

## Consequences

- **§61:** villages look like what they worship, and change as their god, its traits and their
  doctrines change. A village a warlike god conquers grows dark obelisks where its gardens were.
- **Strength:** at the start a god's single domain and its people's mild feeling give a faint style
  (three pieces in a village of a dozen houses). Doctrines, traits and strong love or fear deepen
  it.
- **Not modelled:**
  - organic houses and darker roofs;
  - wildlife drawn about gardens;
  - ceremonies.
