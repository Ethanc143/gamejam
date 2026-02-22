# FOCUS Quick Start

## Run the Vertical Slice

1. Open project in Unity `2022.3+` (URP project).
2. Open any scene (the included sample scene is fine).
3. Press Play.
4. `FocusBootstrap` auto-builds the full runtime slice (vehicle, route, traffic, episodes, replay, analytics).
5. Press `F10` in play mode for accessibility options (distraction intensity, assist buffer, stress intensity, colorblind mode).

## Optional: Author ScriptableObject Episodes

1. Menu: `FOCUS -> Generate Default Distraction Catalog`
2. Edit created assets in `Assets/FOCUS/Resources/DistractionEpisodes/`
3. Assign the catalog to `DistractionManager` if you move or duplicate it.

## Build

1. Add your active scene to Build Settings.
2. Set PC target.
3. Build and run.

The gameplay systems are runtime-composed, so the scene only needs to include camera/light defaults.
