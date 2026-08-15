# GearTierColors

> ## 🤖 Written by AI
>
> **Effectively all of this — the design, the code, the debugging and these docs — was written by
> Claude (Anthropic).** I set the goals, ran it in the game, and reported what broke; the AI did the
> engineering. That includes the parts that were wrong at first, and the fixes for them.
>
> This is stated plainly because you should know what you are running. Read the source before you
> trust it with a profile you care about, and keep backups.

Colours armour, helmets, rigs, plates and headsets by tier in your inventory, the way
[AmmoTierColors](https://github.com/GhostFenixx) colours ammo by penetration.

Built against **SPT 4.1.2**. Server-side only — no client plugin, no Harmony patching. It rewrites
`BackgroundColor` on item templates as the server loads.

## Why it is worth having

Gear background colours currently carry almost no information in 0.16.9.5:

| | before |
|---|---|
| Rigs | **all 66 `default`** |
| Helmets | 102 `default`, 9 assorted |
| Body armour | 39 blue, 3 violet |

So the channel is free. After this mod, colour means armour class at a glance — and the same
palette AmmoTierColors uses, so both mods read as one visual language.

## Tiers

One stat, one colour. Colour is a single dimension: if orange could mean either "class 5" or
"class 6 but heavy" you cannot read it at a glance, so weight is deliberately *not* folded in.

| class | colour |    | hearing | colour |
|---|---|---|---|---|
| 6+ | red | | 67 m | red |
| 5 | orange | | 66 m | orange |
| 4 | violet | | 63 m | violet |
| 3 | blue | | 62 m | blue |
| 2 | green | | 60–61 m | green |
| 1 | grey | | under 60 m | grey |

All of it is in `config.json`, including which categories to touch.

## Two things worth knowing

**A plate carrier is `armorClass 0`.** The plates carry the class, so scoring a carrier on its own
value paints every good one grey — a Slick would read as the worst item in the game. It is coloured
by *the best class it will accept* instead. Turn that off with `ColourCarriersByBestPlate`.

**Headsets have no hearing stat in the database at all.** Every one reads `AmbientVolume -50` and
`DryVolume -60`; the real differences are compressor and EQ curves that do not reduce to a number.
The metre figures come from the [wiki's Earpieces
table](https://escapefromtarkov.fandom.com/wiki/Earpieces) and are matched on the item name, so
they are stated policy from a cited source rather than a derived stat. A headset the table does not
cover is left alone rather than given a rating it never earned.

Worth noting the spread is small: bare ears are 53 m and the ceiling is 67 m, so the bottom band is
worth very little over wearing nothing.

## Installing

Drop the folder into `SPT_Runtime/user/mods/`. Building with `dotnet build -c Release` deploys it
there automatically and will not overwrite a `config.json` you have already edited.

## Licence

MIT. See [LICENSE](LICENSE).
