# Milestone 15, part 21 — Religion's look on villages

## Overview

Part 21 makes religion visibly change villages, as §61 asks. The decisions are in
[ADR-042](../docs/ADR/ADR-042-religions-look-on-villages.md).

What the player sees: a village takes its god's style.

- **Gardens:** a benevolent, loved god of nature, life or plenty brings flower beds and bushes
  among the houses.
- **Monuments:** a fearful war, death or fire god brings dark obelisks and burning braziers about
  the shrine.
- **Lore:** a god of knowledge, justice or protection brings a ring of white columns.
- **Strength:** the style grows with the god's hold, its traits, the village's doctrines and its
  people's love or fear.

## Architecture

| Project | Changes |
| --- | --- |
| `Pantheon.Game` | • `Presentation/ReligiousStyle` (`StyleMotif`, `StylePieceKind`, `StylePiece`, `ReligiousStyles.Of` and `Layout`), `StyleSettings`<br>• `SettlementViewSettings.Style`<br>• F1's style line |
| Godot client | • `Presentation/Settlements/StyleView`, made by `SettlementView`<br>• `WorldPresenter.Style` for F1 |
| Content | `settlement_view.json`: `Style` |

```mermaid
flowchart LR
    god[The village's god: domains, traits] --> score[ReligiousStyles.Of]
    culture[The village's doctrines] --> score
    feeling[Its people's love, fear, faith] --> score
    score --> style[Motif and strength]
    style --> layout[ReligiousStyles.Layout]
    layout --> view[StyleView: flowers, obelisks, braziers, columns]
```

## Test plan

- **`ReligiousStyleTests`:**
  - a loved nature god's gardens, a feared war god's monuments, and a learned faith's columns;
  - no style without a god holding most of the village, and less with a weaker hold;
  - monuments in a ring about the shrine, by turns, clear of the temple and the same each time;
  - gardens among the houses, clear of them, more when stronger, and columns ringing the shrine.
- **`ContentTests`:** a style with too few pieces is rejected.

### Verification results

- **Build and tests:** `dotnet build` has no warnings, and 1401 tests pass (1397 before).
- **On the Mac (seed 42, Brightwater):**
  - with War, two obelisks and a burning brazier by the shrine (120 FPS; the fallback renderer 119);
  - with Nature, flower beds among the houses (112 FPS);
  - with Knowledge, a ring of white columns (115 FPS).
- **Balance:** unchanged, as the style is presentation only.
- **Found on the way:**
  - **A brazier lost to the temple:** the ring skipped the slot on the temple's side, and with three
    pieces that was the one brazier. The ring now spreads its pieces over the free arc.
  - **Too small to see:** at first the pieces could hardly be seen from 75 m, and are now larger.

## Tasks

### Completed

- [x] The style from domains, traits, doctrines and feeling.
- [x] Gardens, monuments and columns laid out and drawn.
- [x] F1's style line, content and tests.

### Future

- Organic houses and darker roofs, wildlife about gardens, and ceremonies.
