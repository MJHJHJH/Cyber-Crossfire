## ADDED Requirements

### Requirement: UI panel preload API parks an instance into the UI instance pool

The system SHALL provide a preload API that loads a UI panel's asset, instantiates it (running `Awake`), parks the instance inactive directly under its own UI group's physical root (selected by the panel config `CanvasMode`; no extra parking node), and registers it into the existing UI instance pool as a locked, free (not spawned) object so a later `OpenAsync` reuses it without reparenting. Preload MUST NOT run any UIForm lifecycle callback (`OnInit` / `OnOpen`). The API SHALL accept a keep-alive parameter with two modes: permanent (kept until explicitly unloaded) and timed (auto-destroyed after N seconds if not opened).

#### Scenario: Preload a panel by panel id

- **WHEN** `PreloadAsync(panelId)` is called for a valid panel that is not yet open
- **THEN** the panel's asset loads through the same loader kind as opening it (YooAsset or Resources from the panel config), an instance is created and parked inactive, registered in the UI instance pool, and locked, and no UIForm lifecycle callback (`OnInit` / `OnOpen`) runs

#### Scenario: Preload runs no lifecycle callbacks

- **WHEN** a panel has been preloaded (asset loaded and instance parked) but not yet opened
- **THEN** `OnInit` and `OnOpen` have not executed for that instance; they run on the first `OpenAsync`

#### Scenario: Preload a panel by location

- **WHEN** `PreloadAsync(location)` is called for a panel without a table config
- **THEN** the Resources loader path is used, consistent with `OpenAsync(location)`

#### Scenario: Preload an already open panel

- **WHEN** `PreloadAsync` is called for a panel that is already open
- **THEN** the call is a no-op: no asset load and no duplicate instance registration

#### Scenario: Preload the same panel twice

- **WHEN** `PreloadAsync` is called twice for the same location
- **THEN** only one parked instance exists and the second call is skipped

#### Scenario: Preload failure is non-fatal

- **WHEN** a single panel fails to preload
- **THEN** the failure is logged, the panel is removed from the preload registry, and the caller is not forced into an error path (a later open falls back to the normal load)

### Requirement: OpenAsync reuses parked preloaded instances without changing its loading path

The existing `OpenAsync` pool-reuse path MUST serve parked preloaded instances unchanged: no new open branch, no asset load, no instantiate. The first open of a parked preloaded instance SHALL run the full lifecycle — `OnInit(userData)` then `OnOpen(userData)` with the caller's `userData` — unlock the instance, and remove it from the preload registry.

#### Scenario: Open after preload completed

- **WHEN** `OpenAsync(panelId)` is called after its preload completed
- **THEN** the open returns through the existing object-pool reuse path with zero asset load and zero instantiate, `OnInit` and `OnOpen` run at open time with the caller's `userData`, the instance is unlocked, and it is removed from the preload registry

#### Scenario: Open while preload is in flight

- **WHEN** `OpenAsync` is called while the same panel is still being preloaded
- **THEN** the open waits for the in-flight preload to settle, then reuses the parked instance; if preload failed, the open proceeds through the normal load path

#### Scenario: Non-preloaded panels unaffected

- **WHEN** `OpenAsync` is called for a panel that was never preloaded
- **THEN** behavior is identical to before this change (including pooled instances that were opened before, which still skip `OnInit` on reopen)

### Requirement: Preloaded instances are protected from auto-release and released on demand

Parked preloaded instances MUST NOT be destroyed by pool auto-release (expire/capacity) while parked. The system SHALL provide single and bulk unload operations that destroy parked instances and release their asset handles, and SHALL support two keep-alive modes: permanent (kept until explicitly unloaded) and timed (auto-destroyed after a configured number of seconds when not opened).

#### Scenario: Pool expiry does not destroy parked instances

- **WHEN** a parked preloaded instance remains unused longer than the pool expire time
- **THEN** it is not auto-released (locked) and remains available for a later open

#### Scenario: Unload a single preload

- **WHEN** `UnloadPreload(location)` is called for a parked instance
- **THEN** the instance is destroyed and its asset handle released

#### Scenario: Unload all preloads

- **WHEN** `UnloadAllPreloads()` is called
- **THEN** every still-parked preloaded instance is destroyed with its asset handle released, and already-opened instances are unaffected

#### Scenario: Timed keep-alive auto-destroys unused preloads

- **WHEN** a panel is preloaded with `keepAliveSeconds = N` and is not opened within N seconds
- **THEN** the parked instance is automatically destroyed and its asset handle released

#### Scenario: Timed preload opened before expiry stops the timer

- **WHEN** a panel preloaded with `keepAliveSeconds = N` is opened before N seconds elapse
- **THEN** it is removed from the preload registry (the timer stops) and follows the normal lifecycle of an opened form

#### Scenario: Permanent preload survives until manual unload

- **WHEN** a panel is preloaded with `keepAliveSeconds <= 0` (permanent)
- **THEN** the parked instance is kept (locked; neither pool expiry nor a keep-alive timer destroys it) until `UnloadPreload` or `UnloadAllPreloads` is called

#### Scenario: Recycle after open follows normal pool rules

- **WHEN** a preloaded instance has been opened (so its `OnInit` already ran) and is later closed and reopened
- **THEN** it is no longer locked, `OnInit` is skipped on the reopen (consistent with normal pooled reuse), and it follows the normal pool expire/capacity recycling rules

### Requirement: Battle entry preloads its UI set during the scene switch

The battle procedure SHALL preload the battle UI set (BattleHud, Joystick, Pause, Lose, Win, GemReward, Message) while the scene switch loading panel is on screen, so that opening these panels after the battle scene is shown does not incur asset load or instantiate cost.

#### Scenario: Preload starts in parallel with scene loading

- **WHEN** the battle procedure invokes `SwitchAsync` with the battle preload list
- **THEN** preload tasks start in parallel with scene loading and are awaited (with a timeout deadline) before the loading panel closes

#### Scenario: Preload timeout fallback

- **WHEN** preload does not finish within the deadline
- **THEN** preload is canceled, parked instances are unloaded, and the battle HUD still opens through the normal load path

#### Scenario: Scene switch failure or cancellation cleans up preloads

- **WHEN** the scene switch fails or is canceled
- **THEN** in-flight preload is canceled and parked instances are unloaded so no preload leaks

#### Scenario: Battle leave releases unused parked instances

- **WHEN** the battle procedure leaves
- **THEN** still-parked (never opened) preloaded instances are unloaded; instances that were opened and closed follow normal pool recycling and may be reused by the next battle
