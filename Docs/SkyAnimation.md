# Dragon and day/night controls

Both are saved in `Assets/Scenes/TCGBoard.unity` and run when you press Play.

Select **Sky Dragon - East to West** to adjust `ValleyDragonFlight`. It crosses right to left in 28 seconds, waits 9 seconds outside the frame, and returns to the right while hidden. Its wings flap and its articulated tail follows a traveling wave. `crossingSeconds`, `pauseSeconds`, `skyHeight`, `distance`, and wingbeat settings are exposed. The flight uses camera viewport coordinates, so resizing the Game view preserves its direction and height. Edit mode displays a representative pose. Its prefab is `Assets/Prefabs/Environment/ValleyDragon.prefab`; meshes and pigments are in `Assets/Art/ValleyDragon`. The palette and silhouette reference `Assets/Art/Cards/Encounters/SmaugAhunt.jpg`.

Select **Valley Day and Night** to adjust `ValleyDayNightCycle`. `cycleSeconds` defaults to **360 seconds** for a complete day. `timeOfDay` also previews the lighting in Edit mode: **0** midnight, **0.25** sunrise, **0.5** noon, **0.75** sunset. Turn off `animate` to hold a chosen time. Sunlight and moonlight strengths are adjustable independently.

The stylized sun and moon paths rise and set across the valley-facing sky. The directional lights rotate with their visible bodies; the active key light switches to the moon at night. Sky, clouds, ambient light, fog, scenery pigment, foliage, water and dragon brightness all change together. Stars fade in after dusk. The custom background shaders receive global lighting parameters; shared material assets and overlay card colors are not rewritten during the cycle. Disabling the controller restores its captured lighting and removes its global tint.

The dragon and cycle live outside the generated landscape root. Rebuilding Golden Vale preserves them and reconnects the cycle to the new sun. The Tools > Background menus can rebuild the dragon or add the cycle without rebuilding the landscape.

Verification: `Tests/DragonFlightChecks.cs` checks leftward progression, sky placement, animated joints, complete off-screen exit/re-entry, and the hidden pause. `Tests/DayNightCycleChecks.cs` checks horizon crossings, moonlight, the key-light switch, ambient/fog/pigment changes, stars, midnight wrapping and shader compilation. Run these through Unity CLI `eval_file`. Day and night previews are `Docs/Dragon-Day.png` and `Docs/Dragon-Night.png`.
