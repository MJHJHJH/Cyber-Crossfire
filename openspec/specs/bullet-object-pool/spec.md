## ADDED Requirements

### Requirement: Bullets spawn and recycle through the framework object pool

All bullet creation and destruction in gameplay code (player weapon and enemy AI fire points, plus the projectile's own range/hit termination) SHALL go through a bullet pool facade backed by the framework `ObjectPoolComponent` (`GameFrameWork.ObjectPool`), with one named single-spawn pool per bullet prefab, lazily created on first use with capacity 64 and expire time 60 seconds. Gameplay fire points MUST NOT call `Instantiate`/`Destroy` directly; pooled instances SHALL be created inactive and registered into the pool on first use.

#### Scenario: Fire player weapon bullets

- **WHEN** the player weapon fires (normal or powered triple shot)
- **THEN** each bullet is spawned from the pool of its prefab with the configured position/forward and projectile parameters, and no `Instantiate` occurs while a free pooled instance exists

#### Scenario: Fire enemy bullets

- **WHEN** an enemy AI (tank, ring shooter, boss) fires any bullet pattern
- **THEN** each bullet is spawned from the pool of its prefab with the same speed/damage/range/forward as before this change

#### Scenario: Pool has no free instance

- **WHEN** a fire point requests a bullet but the pool has no free instance
- **THEN** a new inactive instance is instantiated, registered into the pool, and returned spawned

#### Scenario: Bullet hits or exceeds range

- **WHEN** a bullet hits a valid target or exceeds its range
- **THEN** it is recycled to the pool instead of being destroyed, and the hit-particle/detach behavior is preserved

#### Scenario: Safety lifetime recycle

- **WHEN** a bullet survives beyond its spawn-time safety lifetime (5 s for the player weapon, 10 s for enemy fire points)
- **THEN** it self-recycles as a fallback, matching the old delayed-`Destroy` behavior

### Requirement: Pooled bullet instances are tied to the battle scene lifetime

The framework object pool itself lives in the persistent home scene and SHALL be reachable from any battle scene via `GameFrameWork.ObjectPool`. The pool root and idle bullet instances SHALL live in the battle scene (created on first fire, not `DontDestroyOnLoad`): when the battle scene unloads, idle pooled instances are destroyed with it, and the pool root is rebuilt on the next battle's first fire. The framework `ObjectPoolManager` SHALL remain unmodified; the system SHALL prevent stale entries by fully recycling in-flight bullets and clearing the pool BEFORE the battle scene is unloaded — hooked on `SceneManager.sceneLoaded` (the scene-switch flow loads the new scene before unloading the old one, so battle-scene objects are still alive at that point). A battle-exit recycle (`ClearBattleBullets` on `GameControl.OnDestroy`) and spawn-time self-healing remain as fallbacks for abnormal paths.

#### Scenario: Battle end clears the pool before unload

- **WHEN** the scene switch flow has loaded the next scene (old battle scene not yet unloaded)
- **THEN** in-flight bullets are fully recycled and the pool is emptied while the old-scene objects are still alive, so no entry referencing a destroyed instance remains in the home-scene object pool

#### Scenario: Battle end destroys idle pooled instances

- **WHEN** the battle scene is unloaded
- **THEN** idle (recycled) bullet instances and the pool root are destroyed with the scene; the next battle's first fire recreates the pool root and reuses the empty pools

#### Scenario: In-flight bullets destroyed by an abnormal unload

- **WHEN** the battle scene is unloaded through an abnormal path (no scene switch hook fired)
- **THEN** the battle-exit cleanup (`GameControl.OnDestroy`) force-recycles them safely, and the next spawn self-heals by creating and registering a fresh instance; no exception escapes and gameplay is unaffected

### Requirement: Bullet spawn resets runtime state

Because `Start()` runs only once on a pooled instance, every spawn SHALL reset the projectile's runtime state: restore the prefab's serialized defaults (captured in a per-prefab template on first use), re-initialize the start position and safety-lifetime counter, and clear the recycled flag. Every recycle SHALL clear the creator reference and detach any detach object before reparenting, and SHALL be idempotent (double recycle is a no-op).

#### Scenario: Reused bullet keeps correct team semantics

- **WHEN** a bullet instance is reused by a different owner (e.g., an enemy bullet instance reused by the player weapon)
- **THEN** its team flag (`m_IsEnemyTeam`) and other runtime fields are restored to the prefab defaults before the caller configures them, so player bullets never damage the player and enemy bullets never damage enemies

#### Scenario: Recycle clears cross-scene references

- **WHEN** a bullet is recycled
- **THEN** its creator reference is cleared and its detach object is unparented before the instance is hidden under the pool root

#### Scenario: Double recycle is a no-op

- **WHEN** a bullet both hits and reaches its safety lifetime (or is recycled twice for any reason)
- **THEN** the second recycle is ignored and no pool exception is thrown

### Requirement: Battle exit force-recycles in-flight bullets

The battle controller's destroy path (`GameControl.OnDestroy`) SHALL invoke a battle-cleanup operation that force-recycles all bullets still in flight and then releases unused pooled objects, so the pool holds no ghost entries whose instances were destroyed by scene unload. The operation SHALL tolerate an already-destroyed framework object pool (app quit / editor stop) and SHALL be safe when bullet instances were already destroyed by the unload.

#### Scenario: Exit mid-battle leaves no ghost entries

- **WHEN** the player exits a battle (returns to menu or loads another level) while bullets are in flight
- **THEN** in-flight bullets are force-recycled, unused pooled objects are released, and the next battle reuses the pool with a correct count

#### Scenario: Cleanup runs during app shutdown

- **WHEN** the battle scene is destroyed during app quit or editor play mode stop
- **THEN** the cleanup does not fail when `GameFrameWork.ObjectPool` is unavailable and does not throw
