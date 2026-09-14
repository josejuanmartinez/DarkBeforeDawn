# Retro fantasy presentation

The default board uses brown leather surfaces, brass book corners, parchment-colored lettering and sage player accents. Zone headings and the ascent title use the bundled fantasy display font; small interface text and card rules retain their reading fonts. Surface decoration remains a single input-transparent UI mesh per panel.

The ascent uses authored retro artwork based on the user's carved dungeon-column reference: weathered shrine niches, stone reliefs, winding exterior stairs and lantern-lit balconies. Point-sampled texture strips continue above the view and fade into darkness, showing at least five landings without revealing a summit. The orthographic camera approaches the current landing, where the known character portraits sit whole and still in square frames inside the niches (the board's art zoom cropped them and the card aura's sparks read as stray pixels at this scale, so neither is used here). Unknown landings retain their carved stonework. As the player progresses, the visible section advances up the column.

`TowerMatchController.ascentFinalLevel` controls the eventual end (30 provisionally, or 0 to leave it open-ended). The artwork stops at that landing and the final-landing caption is revealed only on arrival. This is presentation support; additional opponents and playable matches still need their authored content. The dice restore the perspective camera and filtering when the ascent closes.

The final image is `Assets/Resources/Art/EndlessStair.png`; `AgentScripts/ImportStairArtwork.cs` reproduces its import settings. [StairArtworkPrompt.md](StairArtworkPrompt.md) records the built-in image-generation prompts.

`AgentScripts/ApplyFantasyBoard.cs` reproduces the saved default palette through Unity CLI in Edit mode. `AgentScripts/CaptureFantasyTower.cs` captures the establishing view in Play mode after the cinematic initializes; restart Play afterwards because this fixture stops the match coroutine.

Validation: `Tests/EngravedBoardChecks.cs` passed 150 assertions and `Tests/BoardSkinChecks.cs` passed 79 checks after the opening match setup. These cover input transparency, inspection, controls, resized card containment, health, skin switching and collection preservation.

Review images: `Fantasy-Tower.png` and `Fantasy-Board.png`.

## Animated portraits and battle effects

Tower portraits use the same `UI/Zoom` art motion as card previews, with separate motion phases. The portrait frames stay fixed while the artwork drifts, breathes and catches travelling candlelight. `FantasyCardAura` supplies breathing borders and rising embers without changing card transforms or pointer targets.

`BoardBattleVfx` observes the match and card identities. Amber marks actions and selections; ember-red marks committed attackers; sage marks assigned defenders. Curved, animated arrows connect blockers to attackers and unblocked attackers to their targets or the opposing champion. A dashed tether and moving reticle follow the pointer during target selection. Resolved duels produce travelling sparks, impact slashes and floating outcomes; champion health changes produce damage/healing numbers. Drawn and played cards receive a summoning pulse, and stage transitions sweep a small light along the masthead.

Effects use UI geometry and the existing portrait shader, with no imported VFX assets. The battle overlay has no raycaster, all effects ignore pointer input, and it hides during cinematics. Transient bursts, bolts and labels have bounded counts and expire. Recently removed card positions are retained briefly so finishing blows still land where their targets stood. `Tests/FantasyVfxChecks.cs` exercises this behavior in a temporary Play-mode battle and saves review frames; restart Play afterwards.
