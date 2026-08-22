## ADDED Requirements

### Requirement: Sound table is an audio dictionary

The system SHALL maintain Luban `TbSound` rows that describe only audio playback properties (id, location, group, loop, volume, and related play parameters). Sound rows MUST NOT encode which gameplay feature or weapon consumes them.

#### Scenario: Lookup by sound id

- **WHEN** runtime requests a sound configuration by a valid sound id
- **THEN** the system provides the corresponding location, group, and playback parameters from `TbSound`

#### Scenario: Missing sound id

- **WHEN** runtime requests a sound id that is not in `TbSound`
- **THEN** playback MUST fail safely without throwing unhandled exceptions (existing SoundComponent table-miss behavior)

### Requirement: Sound groups route to mixer categories

The system SHALL keep `TbSoundGroup` entries for at least Music and SFX, each mapped to the project AudioMixer group path used by SoundComponent initialization.

#### Scenario: Groups initialize after datatable load

- **WHEN** datatables finish loading and sound groups initialize from table
- **THEN** Music and SFX groups exist and are usable for subsequent `PlaySound` calls

### Requirement: Battle and home BGM are configured and playable by id

The system SHALL include Music-group `TbSound` entries for home BGM and battle BGM with `loop` enabled. Their `location` MUST equal the asset file name without extension (`HomeMusic`, `BGM_Battle`). Both entries MUST set `fade_in_seconds` to 1.

#### Scenario: Enter home procedure

- **WHEN** the main/home procedure finishes loading its scene group
- **THEN** the system plays the home BGM sound id via `PlaySound` (1s fade-in from table)

#### Scenario: Enter battle procedure

- **WHEN** the battle procedure finishes loading the battle scene
- **THEN** the system stops the previous Music BGM with a 1s fade-out and plays the battle BGM sound id (1s fade-in)

#### Scenario: Leave procedure with BGM

- **WHEN** the procedure that started a BGM leaves
- **THEN** that BGM is stopped with a 1s fade-out (by sound id or equivalent location stop) so it does not leak into the next flow

### Requirement: Battle SFX are configured with valid asset locations

The system SHALL include SFX-group `TbSound` entries for player and enemy combat sounds that exist under `Assets/Audio/Characters`, using YooAsset-compatible locations (path without file extension). Invalid or orphaned player placeholder rows MUST be corrected or removed.

#### Scenario: Player combat SFX rows

- **WHEN** datatables are generated after this change
- **THEN** player fire, hit, and death SFX rows reference existing player audio assets under `Audio/Characters/Player/`

#### Scenario: Enemy combat SFX rows

- **WHEN** datatables are generated after this change
- **THEN** enemy launch, explosion, and hit SFX rows remain loadable via their configured locations under `Audio/Characters/Enemy/`

### Requirement: Temporary hardcoded SFX triggers for verification

Until weapon (or other entity) tables reference sound ids, gameplay MAY call `PlaySound` with hardcoded sound ids at combat event points (fire, hit, death) for verification. Those calls MUST use ids present in `TbSound`.

#### Scenario: Fire plays a configured SFX

- **WHEN** a weapon successfully fires during play mode with temporary hardcoded wiring enabled
- **THEN** the configured fire SFX id plays through the SFX group

#### Scenario: Enemy fire uses one hardcoded id in this change

- **WHEN** an enemy weapon fires during the temporary wiring period
- **THEN** the system plays sound id 2001 (not branching across 2001–2003)

#### Scenario: Future weapon table ownership

- **WHEN** a later change adds sound id fields to the weapon table
- **THEN** hardcoded fire SFX ids are expected to be replaced by values read from weapon configuration without changing `TbSound` row semantics
