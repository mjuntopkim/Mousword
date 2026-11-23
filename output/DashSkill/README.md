# Dash animation implementation

Use Left Shift on a character with PlayerSkillController and the existing DashSkill asset.
The controller attaches PlayerDashVisuals automatically; existing scenes and prefabs need no manual wiring.

- Four dedicated poses: anticipation, low dash, cape flutter, recovery.
- 192x40 transparent sheet, four 48x40 cells, Point filtering, no texture compression.
- Original reference palette; original sprite sheet stays unchanged.
- Brief mint-tinted ghosts, ivory/mint wind streaks, ground-only dust.
- 32 pooled effect renderers per player; effects stay in world space and expire.
- Normal sprite animation returns after a 0.07-second visual recovery. Movement resumes immediately when the original dash interval ends.
- Existing speed, duration, mana cost, cooldown and collision detection mode are preserved.

Code changes are limited to the dash section of PlayerSkillController (begin/end animation calls and interrupted-dash cleanup), the new PlayerDashVisuals component, and an editor-only validator. Pre-existing cooldown UI properties are retained. PlayerMove, colliders, DashSkill.asset, other skills, scenes, and the original sprite sheet are not edited.

Run Tools > Mousword > Validate Dash Visuals to check the gameplay prefab and coroutine, left/right poses and velocity, resource import, cleanup and recovery, ground/air effects, pause behavior, and the effect pool. The validator drives the coroutine directly in an isolated additive scene; this is an editor smoke check, not a full keyboard-driven playthrough. Results are saved in validation.txt; actual Unity camera renders are Dash-right.png and Dash-left-air.png.

Artwork was generated with the built-in imagegen tool from mousword_character-Sheet.png. The exact prompt is in prompt.md. PlayerDash-generated.png preserves the generated source; Normalize-DashSheet.ps1 performs reproducible cropping, nearest-neighbor sampling, palette conversion and baseline alignment for Assets/Resources/Dash/PlayerDash.png.
