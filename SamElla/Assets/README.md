# Sam n' Ella pixel art

Death messages are editable in `death-quotes.txt`. Each nonblank, non-comment line is one possible quote beneath the large `YOU DIED!` title. Lines beginning with `;` are comments. There are 42 bundled quotes, including "You bring shame to your family" and "Dishonorable and destitute". Add, remove, or replace quotes, then relaunch the game; long quotes wrap automatically. One quote is randomly selected per death and stays unchanged until respawn, including explosion deaths and the last life.

End-credit quotes are editable in `credits-opening-quotes.txt` and `credits-closing-quotes.txt`. The first file supplies dark-humored motivational quotes about finishing the game; the second supplies play-again quotes. Each nonblank line that does not start with `;` is one choice. Long quotes wrap automatically. One quote from each file is selected when the final level is completed and remains fixed throughout the credits. Existing player and enemy animation sprites are reused for the characters running along the bottom.

Each `.sprite` file is a separate image used by the game. Normal characters and tiles are 16 x 16 pixels; enlarged player frames are 16 x 32. Player frames face right; the renderer flips them when moving left. Character sprites are anchored at their feet and centered over their collision body.

## Editing a sprite

The first non-comment line contains `width height`. Exactly `height` pixel rows follow, each containing exactly `width` symbols. `.` means transparent. Other symbols reference `palette.txt`. Blank lines and lines beginning with `;` are ignored.

For example, this is a valid 4 x 4 image:

```text
4 4
.KK.
KAAK
KTTK
.KK.
```

Colors use six hexadecimal RGB digits, such as `A=57C4B0`. The shared palette has 22 colors. Editing it changes every sprite that uses that symbol. The loader reports invalid dimensions, missing rows, and unknown colors at startup. Sprites may be up to 128 x 128, although tiles should remain 16 x 16 to match the map grid. Normal character and tile checks expect 16 x 16, enlarged player frames expect 16 x 32, overload explosion frames expect 64 x 64, both player and enemy death effects are 24 x 16, and the reward popup can use other dimensions.

Player files: `player-idle.sprite`, `player-run-1.sprite`, `player-run-2.sprite`, `player-jump.sprite`.

Enlarged player files: `player-large-idle.sprite`, `player-large-run-1.sprite`, `player-large-run-2.sprite`, and `player-large-jump.sprite` are editable 16 x 32 sprites. The renderer anchors both sizes at the player's feet. Put `M` in a level to give a question block a growth reward; it uses the same `bonus.sprite` artwork as coin blocks (`?`). Enlarged players shrink when damaged and flash during their brief protection period. Their collision body grows along with the artwork, and growth waits for sufficient headroom.

Growth hearts: `heart-1.sprite` and `heart-2.sprite` are editable 16 x 16 anatomical heart frames, with blood-red muscle, highlights, and blue vessels from the shared palette. The renderer alternates contraction and expansion in a double-beat pulse. Hitting an `M` question block releases a moving, hopping heart; the player must chase and touch it to gain growth and 100 points. Opening an extra growth block does not cause overload until its heart is collected. Hearts reverse at solid obstacles, can fall into pits, freeze while paused or dead, and are cleared on level reload.

Power overload: `player-explosion-1.sprite` through `player-explosion-4.sprite` are editable 64 x 64 frames, displayed in order for 100 ms each. The first frame shows the player swelling to a 24 x 48 body and rupturing; the remaining frames scatter blood, guts, and clothing, then leave a final splatter. Use `E`/`F` for blood, `H`/`S` for organ fragments, and `A`/`T`/`B`/`N` for clothing. All frames share the same canvas and foot anchor and remain inside the visible play area. An extra growth reward beyond the enlarged form triggers this fatal 0.4-second animation, after which the player disappears through the two-second death delay and the subsequent keypress prompt; pausing freezes both timers. The usual player-death and enemy-death splatter sprites remain separate.

Player death effect: `player-splatter.sprite` uses bright/dark blood reds (`E`/`F`), teal clothing (`A`/`T`), blue clothing (`B`/`N`), and gold trim (`Y`). Edit this file to customize the splatter, then restart the game. The effect replaces the living player during the two-second death delay and the subsequent wait for any key to respawn or restart the game. Pit deaths keep the splatter within the visible play area so it does not disappear below the screen.

Enemy files: `enemy-idle.sprite`, `enemy-walk.sprite`.

Toaster Cat files: `toaster-cat-run-1.sprite`, `toaster-cat-run-2.sprite`, and `toaster-cat-jump.sprite` are editable 24 x 16 sprites. The cat faces left by default and flips when moving right. The artwork combines orange cat features, toast, and a shaded metal toaster body. `toaster-cat-squashed.sprite` is a 32 x 16 splatter mixing blood, fur, bread, and steel fragments; it remains visible for one second after death. `C` in a level spawns this enemy. Toaster Cats also join the running characters during the end credits.

Zombie files: `zombie-walk-1.sprite` and `zombie-walk-2.sprite` are editable 32 x 32 canvases showing a player-height zombie with torn clothes, outstretched arms, and a dangling eye. The extra width accommodates the arms. Frames face left, flip when walking right, and shuffle at three frames per second. `zombie-squashed.sprite` is a 32 x 16 blood, green skin, and clothing splatter lasting one second. All frames anchor at the feet. Place `Z` in the bottom tile of a two-tile space to spawn one; bundled zombies start at Level 4 and also join the end credits.

Enemy balancing lives in `enemies.txt`: `[Walker]`, `[ToasterCat]`, `[Zombie]`, and `[Dog]` each have `Speed`, `PowerLevel`, and `HopVelocity` settings. Speeds use tiles per second (0-30), power levels use integers from 1 to 10 and define enemy health, and hop strength ranges from 0 to 20 (zero means no hopping). Defaults preserve ordinary enemies and make Toaster Cats faster, tougher, and bouncy. Use whole-line `;` comments. Relaunch after editing. Missing settings use defaults, while invalid values report the file and line instead of silently changing difficulty.

Enemy death effect: `enemy-squashed.sprite` is a 24 x 16 flattened enemy and blood splatter, using the enemy's original yellow, orange, brown, and blue colors (`Y`/`O`/`R`/`D`/`N`/`W`) mixed with blood reds (`E`/`F`). It remains anchored at the enemy's feet for one second after a stomp or pit death. Enemy and player death effects run independently and can appear together; pausing freezes both timers. Edit either death sprite and restart the game to customize its appearance.

Tile files: `grass.sprite`, `dirt.sprite`, `brick.sprite`, `bonus.sprite`, `used.sprite`, `pipe.sprite`, `coin.sprite`, `spike.sprite`, `goal.sprite`.

Pipes can be one or two tiles wide and any number of tiles tall. Stack `P` tiles vertically, keeping the same width on every row. A stack has one top rim, a continuous stem, and a bottom edge. The renderer automatically chooses the editable 16 x 16 pieces. Use lowercase `p` for an explicitly skinny pipe: its vertical stack stays separate even when touching a wide `PP` pipe. Both `p` and `P` are solid, so a shorter skinny pipe can act as a step beside a tall wide pipe. Keep uppercase-only pipe groups separated if they should not join.

This one-tile skinny step touches a three-tile-tall wide pipe. Jump onto the step, then jump again to clear the wide pipe:

```text
.PP
.PP
pPP
###
```

For example, these are a small pipe three tiles tall and a large pipe four tiles tall:

```text
....PP
P...PP
P...PP
P...PP
######
```

Small stacks use `pipe-small-top.sprite`, `pipe-small-body.sprite`, and `pipe-small-bottom.sprite`. Large stacks use `pipe-top-left.sprite`, `pipe-top-right.sprite`, `pipe-body-left.sprite`, `pipe-body-right.sprite`, `pipe-bottom-left.sprite`, and `pipe-bottom-right.sprite`. Body pieces repeat for extra height; two-tile-tall pipes only need top and bottom pieces. Single-tile pipes, one-row groups, and irregular shapes retain the unchanged original `pipe.sprite` artwork. A large pipe begins at 2 x 2 tiles (32 x 32 pixels); each extra row adds 16 pixels of height. To restore the standard artwork everywhere, copy `pipe.sprite` over the stack-piece sprite files.

Brick break animation: `brick-rubble.sprite` shows broken chunks for 0.25 seconds, followed by `brick-dust.sprite` showing sparse dust for 0.25 seconds. The effect disappears after 0.5 seconds of game time. Both frames are editable 16 x 16 sprites and use the shared brick colors. Pausing freezes the animation. Each broken brick animates independently, and its collision disappears as soon as it breaks. Coin blocks keep their existing reward and used-block behavior.

Reward popup: `reward-plus-one.sprite` is an 8 x 6 pixel image containing the gold `+1` and its shadow. Edit its pixel grid to customize the reward artwork. You can also change its width and height; it stays centered above the rewarded block and floats upward for one second of game time. The popup uses the shared palette, including `Y` for gold and `K` for its shadow. Its dimensions are independent of the 16 x 16 map tiles.

Restart `dotnet run` after editing source art. Running the project copies assets into its build output. For the original plain ZIP, edit the `.sprite` files and palette inside the `Assets` directory beside `SamElla.exe`, then relaunch. Installer and Velopack-managed portable builds use `%LocalAppData%/SamElla/Content/Assets` instead; edits there are preserved during automatic updates. Relaunch after editing.

## Creepy-Crawler

The `creepy-crawler-*.sprite` files are editable 32 x 16 pixel grids using the
shared zombie palette. Buried crawlers are not drawn; the old `buried` mound
asset is retained as an editable reference. `emerge-1` through
`emerge-3` show a hand, head, and torn torso clawing out over one second. The two
`crawl-*` frames alternate as the zombie drags its legless half-body along the
ground. Emergence starts below the terrain and is clipped at the surface as the
body rises. `squashed` is its blood, skin, and torn-clothing splatter. All frames face
left and are mirrored when moving right. They are anchored at the terrain
surface, including supported bricks on the nighttime jumping courses.

Place `R` in the empty tile above solid terrain on a nighttime map. The bundled
campaign introduces it on Level 5. `[Creepy-Crawler]` in `enemies.txt` controls its
speed and health independently of the standing zombie; default speed is 1.5,
health is 2, and hopping is disabled.

## PNG previews

`Previews` contains standard transparent PNG exports for inspecting or sharing the art. They are generated from the `.sprite` sources; editing a preview PNG does not change the game. From the SamElla project directory, regenerate them with:

```powershell
dotnet run -- --export-art Assets/Previews
```

`sprite-sheet.png` shows all sprites in alphabetical filename order, numbered from 1. `game-preview.png` is a frame produced by the same renderer used in the game window. PNG exports use only .NET compression, with no image library dependency.

Player balancing is also in `enemies.txt` under `[Player.PowerLevel1]` (small) and `[Player.PowerLevel2]` (large). Each has `Speed`, `PowerLevel`, and `HopVelocity`. Player power is stomp damage, running speed uses tiles per second, and hop velocity controls normal jump takeoff. Small power defaults to 1 and large power to 2; both forms keep speed 12 and jump velocity 15. Thus a default Toaster Cat (health 2) takes two small-player stomps or one large-player stomp. Settings switch when growing or shrinking, and files without player sections retain these defaults.

Ella files: `ella-idle.sprite`, `ella-run-1.sprite`, `ella-run-2.sprite`, and `ella-jump.sprite` are editable 16 x 16 images. Their `ella-large-*` variants are 16 x 32. Ella wears purple with a ponytail; palette symbols P/Q/U/J provide purple shades. Her separate `ella-splatter.sprite` and `ella-explosion-1.sprite` through `ella-explosion-4.sprite` mix purple clothing with the existing blood colors. Select Ella on the title screen with A/D or Left/Right, then use Up/Down to select Start Game and press Enter. Sam keeps the original `player-*` files. Both characters share physics and power settings.

Dog files: `dog-walk-1.sprite` and `dog-walk-2.sprite` are 24 x 16 frames of a brown, floppy-eared dog with a red collar and wagging tail. They face left and flip when moving right. `dog-squashed.sprite` is a 24 x 16 blood-and-fur splatter that lasts one second. `poop.sprite` is a 16 x 16 smiling poop emoji anchored to the ground. `poop-screen-splatter.sprite` is a 128 x 96 screen overlay, blended at 55% opacity for two seconds after stepping in a pile. Edit the sprites and relaunch to customize them. The five-second slowdown halves running speed for Sam or Ella in either size; a fresh pile refreshes the effect. Dogs join the animated title screen and end credits.

Flamethrower files: `flamethrower.sprite` is the editable 16 x 16 handheld tank, grip, and barrel, facing right and flipped when aiming left. It is drawn over either character. `flamethrower-pickup.sprite` is the separate 16 x 16 collectible, released by an `F` question block and chased like a growth heart. `flame-1.sprite` through `flame-4.sprite` are editable 16 x 16 fire textures tiled along a stream up to three blocks long, with 100 ms per frame. Solid terrain clips the stream, and left-facing flames are mirrored. Only large players can collect it; small players leave it untouched. Hold X or Ctrl to fire. Damage is the current stomp strength plus one, with a 0.3-second damage interval. A burst lasts at most one second; release and press fire again to restart. The first hit is immediate, and the damage cooldown persists between bursts to prevent rapid-tap damage. Pausing freezes fire animation and damage. The artwork uses the existing steel, red, orange, yellow, and white palette colors.

Duplicate flamethrower burn: `player-burning-1.sprite` through `player-burning-4.sprite` are shared 24 x 40 overlays for Sam and Ella. They face forward around the player and anchor at the feet. The renderer blends them at 78% opacity and changes frames every 100 ms for three seconds. A duplicate weapon is consumed and removes the equipped flamethrower immediately; the player keeps their large form and lives. These fire overlays are separate from the existing fatal growth-heart explosion. Flamethrower blocks start in Level 2.

Flamethrower pickup quotes: `flamethrower-quotes.txt` contains ten editable messages, including Light Em' If you Got Em! and Make Flamethrowers Great Again. Put one quote on each line without surrounding quotation marks; blank lines and lines starting with `;` are ignored. Relaunch after editing. Equipping the weapon picks one message at random and shows it in a centered, wrapping banner for three seconds. Pausing freezes the timer. Restarting, changing levels, dying, or losing the weapon clears it; a duplicate pickup triggers the burn penalty instead of a new success quote. Missing or empty files use the first requested quote as a fallback.

Night scenery: `moon.sprite` is a 32 x 32 cratered moon. `tombstone.sprite` and `tombstone-cross.sprite` are 16 x 24 gravestones. Edit these pixel grids and relaunch to customize the night background on Levels 4, 5, 8, and 9. Gravestones are drawn behind level tiles and characters, anchored near the ground and repeated in two scrolling layers; they have no collisions. Each level chooses Day or Night with its first-line `Background=Day` or `Background=Night` header. The bundled Levels 4, 5, 8, and 9 select Night, and the rest select Day. Files without the header default to Day. The title screen retains its daytime background.

Level-start advice is editable in `uncle-quotes.txt`. The file contains 30 demotivating lines, including Try not to die this time and You dropped your pocket. A level-start page chooses one quote and displays the level number, filename-derived name, and remaining lives for five seconds, or until a fresh keypress. Gameplay freezes during the page; losing focus freezes its countdown. The page appears before the first level, subsequent levels, and retries.

Final-life roasts are editable in `game-over-quotes.txt`, which contains 15 messages. After the last death animation finishes, a full GAME OVER page displays one random quote until the player presses a fresh key to try again. Regular death quotes remain in `death-quotes.txt`. Both new files use one quote per line; blank lines and `;` comment lines are ignored. Missing or empty files use fallback messages. Long messages wrap and truncate within the page. Relaunch after editing.

Death restart input is accepted immediately: a fresh keypress during any death animation reloads the level, or starts a new run after the last life. Holding a key does not count. With no new keypress, the death sprites and animation timing remain unchanged, followed by the normal continue prompt or game-over page. The prompt appears in the footer during the animation so it does not cover the death artwork.

Boom-Stick! files: `boom-stick.sprite` is the 16 x 16 wood-stocked shotgun, drawn over Sam or Ella and mirrored when aiming left. `boom-stick-pickup.sprite` is its separate 16 x 16 collectible. `shotgun-blast-1.sprite` and `shotgun-blast-2.sprite` are 16 x 16 muzzle-flash/pellet textures tiled along the shot, shown for 100 ms each. Put `Q` in a map to release the shotgun from a question block; the bundled levels introduce it at Level 4. Collecting it shows "This here is my BOOM STICK!" for three seconds. It requires a large player, replaces the equipped flamethrower, reaches three tiles, breaks bricks, and deals flamethrower damage plus one. Fire once per X/Ctrl press with a 0.5-second reload. Taking a hit removes the shotgun while leaving the player large; no flamethrower is granted. Edit the sprites and relaunch to customize the artwork.

Old Man boss: `old-man-walk-1.sprite`, `old-man-walk-2.sprite`,
`old-man-windup.sprite`, `old-man-swing.sprite`, and `old-man-squashed.sprite`
are editable 32 x 32 frames showing white hair, spectacles, a blue jacket,
and a wooden cane. They face left and flip to face right. The air wave is
drawn in white and cyan and reaches two tiles from the cane side of his body.

Underworld art: `sewer-brick.sprite` and `sludge.sprite` are 16 x 16 terrain.
Sewage mains, drains, leaks, and sludge bubbles are drawn by the renderer.
`fly-1/2.sprite` are 16 x 16 wing frames; `worm-1/2.sprite` are 24 x 16 crawling
frames. Each has a matching `-squashed.sprite`. The `punk-blue-*` and
`punk-purple-*` sets are 24 x 32 walking, throwing, and flattened frames with
colored mohawks; `emo-boss-*` is a larger 32 x 48 set with a long purple fringe.
`beer-bottle.sprite` and `needle.sprite` are 16 x 16 projectiles. Actors face left
and flip right. Punk and boss taunts come from `enemy-quotes.txt`; the death groan is
synthesized in `GameSounds` and requires no downloaded sound file.

Chain whip assets: `chain-whip.sprite` is the 16 x 16 handheld coiled weapon;
`chain-whip-pickup.sprite` is the collectible released by a `W` block.
`chain-link-1.sprite` and `chain-link-2.sprite` are 16 x 16 metal link textures
used along the extending/retracting chain. All use the existing shared palette.
The crack sound is synthesized by `GameSounds`.

Speed Runs assets: `pepto-bottle.sprite` is a 16 x 16 pink medicine bottle using
the new `I` palette color. `toilet.sprite`, `player-toilet-sit.sprite`, and
`ella-toilet-sit.sprite` are editable 32 x 32 bathroom frames. The stall rail,
closing purple curtain, occupied sign, and pipe relief puffs are drawn by the
renderer. Two distinct short/long fart sounds are synthesized in `GameSounds`.
