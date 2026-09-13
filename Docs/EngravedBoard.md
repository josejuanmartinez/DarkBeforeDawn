# Engraved board presentation

The board furniture uses a single procedural mesh per surface: cut corners, inset metalwork, soft shading and small compass ornaments. Large panels remain translucent and the atmosphere veil is reduced so the animated level remains visible. Card stock, health tracks and temporary scorecards use stronger contrast for readability. No new image downloads, materials or shaders are required.

Both champions have live health bars with numeric values, an amber damage trail and a red low-health state. The scale follows the match's starting health. Travel temporarily replaces the top land/destination lane with landscape artwork, a route, terrain, destination and the travelling champion's portrait. The automatic turn flow waits for the portrait to reach the stop; player combat actions remain available. The underlying lane is restored when travel closes.

Initiative uses rounded ivory and slate dice with flush pips and a shared framed result display. The render camera follows the viewport aspect so the dice retain cubic proportions. Resolved combat displays a seven-second scorecard in the world lane: combatants, attack plus roll, totals, damage and outcome. For simultaneous duels, the scorecard identifies the last duel and the total number resolved.

`Tests/EngravedBoardChecks.cs` checks input transparency, inspection, toolbar placement, button raycasts, card containment at three zone sizes and live health. Run through Unity CLI `eval_file` after the opening animation. `Tests/BoardSkinChecks.cs` also checks repeated skin changes and collection preservation against the new surface renderer.

`AgentScripts/PreviewBoardPresentation.cs` is a temporary Play-mode visual fixture for travel, damage and combat. Restart Play after running it to restore the real match. `AgentScripts/ApplyEngravedBoard.cs` reproduces the saved default palette in Edit mode.
