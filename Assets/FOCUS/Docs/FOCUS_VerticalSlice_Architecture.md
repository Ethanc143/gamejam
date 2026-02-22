# FOCUS Vertical Slice Architecture

## System Diagram (UML-Style Text)

```text
[FocusBootstrap]
    -> creates [World], [PlayerVehicle], [Traffic], [Systems]
    -> wires references across managers

[VehicleInputSource] --> [VehicleController] <-- [CognitiveLoadManager]
                                   |
                                   +--> publishes control state to [DriveTelemetryRecorder]

[DistractionManager] --(DistractionLifecycleEvent)--> [FocusEventBus]
        |                                            |
        +--> affects [CognitiveLoadManager] <--------+
        +--> feeds eyes-off-road context to [RiskAssessmentSystem]

[TrafficSpawnManager] --> spawns [TrafficVehicleAI] on [LanePath]
[TrafficVehicleAI] --> registers in [TrafficRegistry]

[RiskAssessmentSystem]
    -> reads [TrafficRegistry] + [VehicleController] + [DistractionManager] + [CognitiveLoadManager]
    -> computes TTC, near miss, collision, hard braking, risk value
    -> emits [RiskSnapshot] + [ReplayTriggerEvent] via [FocusEventBus]

[ReplayRecorder] <-- samples [VehicleController] + [CognitiveLoadManager]
[ReplayDirector] <-- listens for [ReplayTriggerEvent]
    -> uses Timeline/PlayableDirector for replay moments

[DriveTelemetryRecorder] <-- listens to [RiskSnapshot], samples per-frame state
[DriveScoringSystem] <-- applies [ScoringProfile] weights to telemetry summary
[PostDriveAnalyticsUI] <-- receives final summary
[FocusGameManager] --> ends session, resolves outcome, presents analytics
```

## Folder Structure

```text
Assets/FOCUS/
  Docs/
    FOCUS_VerticalSlice_Architecture.md
  Scripts/
    Accessibility/
      AccessibilitySettings.cs
    Cognitive/
      CognitiveLoadManager.cs
    Core/
      FocusBootstrap.cs
      FocusEvents.cs
      FocusGameManager.cs
    Distraction/
      DistractionEpisodeCatalog.cs
      DistractionEpisodeDefinition.cs
      DistractionManager.cs
      Editor/
        DistractionCatalogSeeder.cs
    Replay/
      ReplayDirector.cs
      ReplayRecorder.cs
    Risk/
      RiskAssessmentSystem.cs
    Scoring/
      DriveScoringSystem.cs
      ScoringProfile.cs
    Telemetry/
      DriveTelemetryRecorder.cs
      TelemetryModels.cs
    Traffic/
      LanePath.cs
      PedestrianAgent.cs
      TrafficLightController.cs
      TrafficRegistry.cs
      TrafficSpawnManager.cs
      TrafficVehicleAI.cs
    UI/
      FocusHUD.cs
      PostDriveAnalyticsUI.cs
    Vehicle/
      CameraFollowRig.cs
      VehicleAudioController.cs
      VehicleController.cs
      VehicleInputSource.cs
```

## Script Relationship Summary

- `FocusBootstrap` is the composition root; it creates a complete playable vertical slice from an empty scene.
- `FocusEventBus` is the event backbone between distraction, cognitive load, risk, replay, and UI.
- `DistractionEpisodeDefinition` and `DistractionEpisodeCatalog` define modular episode data using ScriptableObject configuration.
- `DistractionManager` executes episodes non-blocking while driving continues.
- `CognitiveLoadManager` applies blended steering precision loss, input delay, brake delay, and peripheral suppression.
- `RiskAssessmentSystem` computes TTC, near misses, hard braking, collisions, and aggregate risk spikes.
- `ReplayRecorder` + `ReplayDirector` provide contextual replay when risk spikes, near misses, or collisions happen.
- `DriveTelemetryRecorder` and `DriveScoringSystem` transform raw simulation into measurable player-specific outcomes.
- `PostDriveAnalyticsUI` delivers personalized, non-moralizing feedback based on telemetry deltas.

## Vertical Slice Controls

- `W/A/S/D`: throttle / steer / brake
- `Space`: handbrake
- During distraction:
  - `1` Ignore event
  - `2` Engage event
- End screen: `R` restart current scene

## Authoring ScriptableObject Episodes

1. In Unity, run: `FOCUS -> Generate Default Distraction Catalog`
2. Edit assets in `Assets/FOCUS/Resources/DistractionEpisodes/`
3. Tune trigger windows, load, risk multiplier, and telemetry impact

If no catalog exists, runtime fallback episodes are generated so the slice remains playable.
