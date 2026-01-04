# MLB Showdown - Card Battle Baseball

A turn-based two-player card game based on MLB Showdown, built with Unity and Photon Fusion networking. Features 3D physics-based dice rolling in a 2D environment.

## Game Overview

This is a stat-based tabletop-style baseball simulation where:

- Dice rolls combined with player stats determine advantage
- Outcome cards resolve each at-bat
- Situational plays (steals, sac flies, double plays) add tactical decisions
- Full stat tracking for batters and pitchers

## Project Structure

```
Scripts/
├── Core/
│   ├── GameState.cs          - Game state enums and types
│   ├── GameSetup.cs          - Scene setup helper
│   ├── GameBootstrap.cs      - Full scene bootstrapper
│   ├── SceneInitializer.cs   - Lightweight scene initializer
│   ├── AtBatController.cs    - At-bat logic handler
│   ├── OptionalActionController.cs - Steal/tag-up/double play logic
│   ├── GameStatistics.cs     - Box score and stats tracking
│   └── CPUPlayer.cs          - AI opponent logic
│
├── Network/
│   ├── NetworkGameManager.cs - Main networked game controller
│   ├── NetworkRunnerHandler.cs - Photon Fusion session management
│   └── PlayerController.cs   - Networked player representation
│
├── Cards/
│   ├── CardData.cs           - Batter/Pitcher card definitions
│   └── CardDatabase.cs       - Sample player cards database
│
├── Dice/
│   ├── DiceRoller3D.cs       - Networked dice rolling
│   ├── DicePhysicsSetup.cs   - Table and walls for dice
│   ├── D20Mesh.cs            - Icosahedron mesh generator
│   ├── DiceVisualizer.cs     - Dice visual effects
│   └── DicePrefabCreator.cs  - Runtime prefab creation
│
├── BaseRunning/
│   └── BaseRunnerController.cs - Runner advancement logic
│
└── UI/
    ├── GameUI.cs             - Main game HUD
    ├── MainMenuUI.cs         - Main menu and lobby
    ├── CardDisplayUI.cs      - Player card display
    ├── OutcomeCardUI.cs      - Outcome chart display
    ├── BaseRunnerDisplay.cs  - Diamond base indicator
    ├── LineupDisplayUI.cs    - Team lineup display
    ├── DiceRollUI.cs         - Dice result display
    ├── BoxScoreUI.cs         - Box score display
    └── GameOverUI.cs         - End game screen
```

## Setup Instructions

### 1. Scene Setup

Add the `SceneInitializer` component to an empty GameObject in your scene. It will automatically create:

- Network handler
- Dice physics system
- UI canvas and components
- Camera and lighting

Alternatively, use `GameBootstrap` for a more complete setup with pre-built UI.

### 2. Photon Fusion Configuration

1. Open the Photon Hub (Fusion > Fusion Hub)
2. Enter your App ID from the Photon Dashboard
3. Configure the NetworkProjectConfig asset

### 3. Network Prefabs

Create prefabs for:

- **GameManager**: Add `NetworkGameManager`, `BaseRunnerController`, `DiceRoller3D`
- **Player**: Add `PlayerController`

Register these in the NetworkProjectConfig.

## Game Flow

1. **Team Assignment** - Both players roll to determine Home/Away
2. **Lineup Setup** - Random 9-batter lineups + pitcher assigned
3. **At-Bat Cycle**:
   - Defense rolls D20 + Pitcher Control vs Batter OnBase
   - If defense ≤ OnBase: Batter has advantage
   - Offense rolls D20 on appropriate outcome card
   - Result determines hit/out/walk
4. **Optional Actions** - After certain outs:
   - Strikeout → Stolen base attempt
   - Flyout → Tag-up (sac fly)
   - Groundout → Double play
5. **Inning Management** - 3 outs switches sides, 9 innings total

## Player Card Stats

### Batter

| Stat         | Purpose                           |
| ------------ | --------------------------------- |
| OnBase       | Target for advantage check (1-20) |
| Speed        | Steal/tag-up attempts (1-20)      |
| PositionPlus | Defensive bonus (0-5)             |
| OutcomeCard  | D20 ranges for each result        |

### Pitcher

| Stat        | Purpose                               |
| ----------- | ------------------------------------- |
| Control     | Added to defense roll (1-10)          |
| Innings     | Stamina limit (1-9)                   |
| OutcomeCard | D20 ranges when pitcher has advantage |

## Multiplayer Modes

- **Host Game** - Create a room for another player to join
- **Join Game** - Connect to an existing room
- **VS CPU** - Play against AI opponent

## Key Classes

### NetworkGameManager

Central game controller handling:

- Game state machine
- Turn management
- Score tracking
- Network synchronization

### DiceRoller3D

Physics-based dice rolling with:

- 3D physics simulation
- Networked result synchronization
- Visual feedback

### BaseRunnerController

Manages runners on base:

- Advancement on hits/walks
- Scoring
- Optional action resolution

## Customization

### Adding New Players

Edit `CardDatabase.cs` to add new batter/pitcher cards with custom stats and outcome ranges.

### Adjusting AI

Modify `CPUPlayer.cs` thresholds:

- `minSpeedForSteal` - Minimum speed to attempt steals
- `stealAttemptChance` - Base probability for steal attempts
- `tagUpChance` - Probability for tag-up attempts

## Dependencies

- Unity 2021.3+
- Photon Fusion 2.x
- TextMeshPro (included with Unity)
