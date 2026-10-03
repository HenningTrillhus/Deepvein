DEEPVAIN PLAYER - UNITY SETUP
=============================

Files (put the whole PlayerV2 folder under Assets, e.g. Assets/DeepVain/PlayerV2):
  Layers/                      the layer sheets (png)
  Unity/LayerSpriteSet.cs      one asset per layer: the sprites of every animation
  Unity/PlayerLayerSync.cs     sets every layer's sprite from animId + frame (same Animator for all layers)
  Unity/PlayerPalette.cs       skin / hair / clothes colours (palette swap)
  Unity/PlayerPaletteSwap.shader
  Unity/Editor/PlayerSetupTool.cs

Steps
 1. Copy the folder into Assets. Wait for Unity to compile.
 2. Menu DeepVain > Player Setup. Check the paths, set Pixels Per Unit (same as your tiles / goblin), press Build.
 3. Build slices all sheets (pivot at the feet), creates Generated/ with:
      LayerSets/*.asset, Clips/*.anim, Player.controller, PlayerPaletteSwap.mat, Player.prefab
 4. Drag Player.prefab into the scene. Tick "female" on PlayerLayerSync for the female face/hair.
 5. Drive the Animator from your movement script:
      animator.SetFloat("Speed", Mathf.Abs(velocity.x));
      animator.SetFloat("VelY", velocity.y);
      animator.SetBool("Grounded", grounded);
      animator.SetBool("Climbing", onLadder);
      animator.SetBool("Dead", dead);
      animator.SetTrigger("Jump"); animator.SetTrigger("Attack"); animator.SetTrigger("Hit");
    Turn the player by flipping scale.x on the root (-1 = left).

Colours: PlayerPalette on the prefab (skin / hair / shirt / shorts, 5 colours each),
         or PlayerPalette.SetSkinPreset(i) / SetHairPreset(i) from code.

Armour: make another LayerSpriteSet for the armour piece (same animation names / frame counts as the body)
        and call  playerLayerSync.SetEquipment("Torso", armourSet);  - the animations do not change.

Notes
 - Clips contain no sprites. They only animate PlayerLayerSync.animId and .frame.
   Keep transitions at duration 0 (the tool does) - blending those two numbers makes no sense.
 - The shader is a plain sprite shader. It does not react to URP 2D lights; if you need lit sprites,
   tell me and I will make a Shader Graph version.
 - Attack and Death frames are wider (32 / 44 px). The tool sets the pivot at the feet for each size.
