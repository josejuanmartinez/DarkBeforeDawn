# Golden Vale backdrop

`Assets/Scenes/TCGBoard.unity` opens with **The Golden Vale** active. Its palette and composition draw from the Rivendell, Shire, and Dawn illustrations in `Assets/Art/Cards`: teal shadow ink, moss and ochre foliage, warm limestone, and fine print grain.

The scene contains layered mountain ranges, a spired citadel, an arched bridge, a flowing jade river and hillside cascade, woodland, ferns, and foreground oaks. Geometry and materials live in `Assets/Art/RetroValley`. The previous **The Greenwood Marches** root is retained, disabled.

The quality revision replaces rounded crowns and cone firs with alpha-cutout illustrated foliage arranged on 3D branches. It adds terrain and bark texture detail, ground shadows, fractured rock shapes, and a cooler sky. The castle now rests on a shaped hill with retaining walls. A 64-step masonry-supported approach connects its gate to a ground-following path toward the bridge. `Tests/ValleyApproachChecks.cs` raycasts each stair foundation against the actual terrain mesh; run it with Unity CLI `eval_file` after rebuilding in Edit mode.

## Illustrated foliage asset

`Assets/Art/RetroValley/IllustratedOak.png` was generated with the built-in image-generation tool and imported with transparency, mipmaps, and trilinear filtering. The shader clips its alpha so transparent gaps remain open between leaves. No generated scenery image replaces the 3D scene.

Generation prompt: “Use case: stylized-concept. Production game texture asset, a single isolated leafy oak bough on a truly transparent background, square canvas 1024x1024. This is an alpha-cutout foliage texture to place many times around three-dimensional tree branches in a vintage illustrated fantasy game. A dense irregular asymmetrical mass of many tiny distinct oak leaves attached to a few thin twigs. Entire spray fits inside image with empty transparent margins 6 percent, broad overall silhouette 1.25 times wider than tall, irregular holes through leaves, intricate serrated edges. Rich dark teal shadow foliage with moss green midtones and muted ochre golden leaf tips, warm directional light from upper left. Hand-painted 1970s fantasy book illustration with etched black ink line details and lithographic pigment texture, botanical sophistication, natural detailed leaves. Flat frontal view of the leafy spray, no perspective ground, no whole tree, no trunk, no scenery, no border, no lettering, no shadows outside foliage. Actual transparent alpha outside leaves and through gaps, no checkerboard.”

The returned texture is 1536 × 1024 with alpha. The exact prompt is recorded above even where the returned dimensions differed from the request.

The camera's `RetroValleyCamera` component controls the composition and gentle bounded drift. Adjust `position`, `focus`, and `drift` there; its `Apply` method previews the pose. The previous full orbit component is disabled. Water and foliage animation run in the shaders. The existing retro fullscreen material affects the background camera; cards use the overlay canvas.

To regenerate the authored geometry, open TCGBoard outside Play mode and choose **Tools > Background > Rebuild Golden Vale**. The builder uses a fixed seed and updates generated meshes and materials in place, preserving their asset GUIDs. Rebuilding replaces edits within the generated root and resets its materials and camera composition. Edit the builder to retain changes across rebuilds.

Verified in the connected Unity Editor: shader compilation, saved mesh/material references, all 12 board zones, 1,137 existing board presentation assertions, and the existing 36 responsive zone sizes plus four edge-preview checks. Screenshot previews render the background camera; screen-space overlay cards are not included by the CLI camera screenshot command.
