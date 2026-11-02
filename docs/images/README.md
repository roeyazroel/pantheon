# Screenshots

The README's and the website's screenshots, taken by the game itself at 1600 × 900 from seed 1234
and saved as JPEG (quality 82). [DEVELOPMENT.md](../DEVELOPMENT.md#taking-the-readmes-screenshots)
shows how to take them, on a machine without a GPU too. Each was taken with these options, after `--seed=1234`:

| Image | Options |
| --- | --- |
| `pantheon.jpg` | `--pantheon=on --angel=off --hour=15 --quit-after-frames=50` |
| `talk.jpg` | `--gods-met=on --talk=2 --angel=off --advance-ticks=1200 --hour=15 --quit-after-frames=50` |
| `proposal.jpg` | `--gods-met=on --talk=3 --angel=off --advance-ticks=3000 --hour=15 --quit-after-frames=50` |
| `minds.jpg` | `--title-page=minds --quit-after-frames=50` |
| `angel-radiant.jpg` | `--angel=radiant --advance-ticks=1200 --hour=15 --quit-after-frames=50`, cropped to the angel (x 440–1130, y 160–500) |
| `angel-torn.jpg` | `--angel=torn --advance-ticks=1200 --hour=15 --quit-after-frames=50`, cropped the same way |
| `angel-fallen.jpg` | `--angel=fallen --advance-ticks=1200 --hour=15 --quit-after-frames=50`, cropped the same way |
| `angel-kind.jpg`, `angel-unformed.jpg`, `angel-stern.jpg` | `--angel=kind`, `--angel=unformed` or `--angel=stern`, with `--advance-ticks=1200 --hour=15 --quit-after-frames=50`, cropped the same way (the website only) |
| `village.jpg` | `--advance-ticks=9000 --camera-distance=110 --angel=off --hour=15 --quit-after-frames=50` |
| `title.jpg` | `--menu=on --hour=15 --quit-after-frames=60` |
| `creature.jpg` | `--focus=creature --angel=off --hour=15 --quit-after-frames=50` |
| `reward.jpg` | `--focus=creature --judge=reward --camera-distance=46 --angel=off --advance-ticks=9000 --hour=15 --quit-after-frames=36` |
| `fight.jpg` | `--meet=on --fight=on --focus=creature --angel=off --hour=15 --quit-after-frames=90` |
| `eruption.jpg` | `--disaster=eruption --angel=off --hour=18.1 --quit-after-frames=480` |
| `storm.jpg` | `--weather=hurricane --camera-distance=140 --angel=off --advance-ticks=600 --hour=15 --quit-after-frames=90` |
| `fire.jpg` | `--disaster=fire --camera-distance=220 --angel=off --advance-ticks=1500 --hour=18.1 --quit-after-frames=120` |
| `beast.jpg` | `--great-event=beast --angel=off --hour=15 --quit-after-frames=90` |
| `religion.jpg` | `--overlay=religion --camera-distance=1500 --weather=normal --angel=off --advance-ticks=3000 --hour=15 --quit-after-frames=50` |
| `diplomacy.jpg` | `--gods-met=on --diplomacy-panel=on --angel=off --advance-ticks=3000 --hour=15 --quit-after-frames=50` |
| `book.jpg` | `--book=world --angel=off --advance-ticks=3000 --hour=15 --quit-after-frames=50` |
| `autumn.jpg` | `--season=autumn --focus=3 --camera-distance=170 --advance-ticks=9000 --angel=off --hour=15 --quit-after-frames=150` |
| `winter.jpg` | `--season=winter --camera-distance=130 --advance-ticks=9000 --angel=off --hour=15 --quit-after-frames=150` |
| `dusk.jpg` | `--camera-distance=200 --advance-ticks=9000 --angel=off --hour=18.1 --quit-after-frames=150` |
| `night.jpg` | `--season=winter --focus=8 --camera-distance=90 --advance-ticks=9000 --angel=off --hour=22.5 --quit-after-frames=150` |
| `miracle.jpg` | `--divine-power=9000 --cast=heal --camera-distance=90 --advance-ticks=9000 --angel=off --hour=15 --quit-after-frames=26` |

Each holds an hour with `--hour`, as ticks now move the day on: tick 9000 is 3 in the morning (ADR-079).
No model was chosen, so the offline scribes wrote and spoke for the gods. The rival gods' names
come from the world's seed, so the same options give the same gods. What
happens in the frames before the screenshot depends on how fast the computer runs them, so a
retake is close to, but not always exactly, the same picture.
