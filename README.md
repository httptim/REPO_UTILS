# REPO_UTILS

A comprehensive MelonLoader utility mod for R.E.P.O that enhances gameplay through ESP features, player modifications, item manipulation, and detailed game information.

![REPO_UTILS](https://i.imgur.com/oeVNQ3x.jpeg)

## Features

### Player Enhancements
- **God Mode**: Become invincible with enhanced sprint speed (`SprintSpeed = 8`) and infinite stamina (`EnergyCurrent = 100`).
- **Player ESP**: Visual tracking lines for other players in multiplayer sessions.
- **Player Management**:
    - Heal Self (Uses `PlayerHealth.Heal(10, false)`).
    - Revive Self (Uses `PlayerHealth.Heal(100, false)`).
    - Heal Other Player (Uses `PlayerHealth.Heal(10, false)` on target).
    - Revive Other Player (Uses `PlayerHealth.Heal(100, false)` on target).
    - Displays actual player names (`PlayerAvatar.playerName`) instead of generic labels.
- **Removed**: Kill Player functionality.

### Enemy Management
- **Enemy ESP**: Visual tracking lines to all enemies in the level.
- **Enemy Information**: Real-time distance tracking and status monitoring.
- **Enemy Control**: Instantly eliminate enemies from a distance using the "Kill" button in the UI.

### Item Features
- **Item ESP**: Visual tracking lines to all valuable items.
- **Item Information**: Distance tracking and total value calculation (`ValuableObject.dollarValueCurrent`).
- **Closest Item Max Value**: Sets the value of the closest item to 999,999 using the `ValuableObject.DollarValueSetRPC(float)` method.
- **Make All Items Cheap**: Sets the value (`ItemAttributes.value`) of all items under `Level Generator/Items` to 1.

### Extraction Point Management
- **Complete Extraction Points**: Sets the state of all extraction points under `Level Generator/Level` to `Complete` by calling `ExtractionPoint.StateSet(State.Complete)` or `ExtractionPoint.StateSetRPC(State.Complete)`.

### Additional Tools
- **Advanced Logging System**: Monitors game state changes and component values.
- **Game Structure Analysis**: Maps the entire game hierarchy for easier modding.
- **Streamlined UI**: Toggle-based interface for features and action buttons.
- **Keybind Support**: Quick access to toggle functions.

## Installation

1. Make sure you have [MelonLoader](https://github.com/LavaGang/MelonLoader) installed (v0.5.7 or higher recommended).
2. Download the latest release of `REPO_UTILS.dll` from the Releases page (link needed).
3. Place the `REPO_UTILS.dll` file in your `R.E.P.O/Mods` folder.
4. Launch the game.

## Usage

### Key Bindings

| Key             | Function                       |
| --------------- | ------------------------------ |
| `;` (Semicolon) | Toggle God Mode                |
| `L`             | Toggle Enemy ESP               |
| `I`             | Toggle Item ESP                |
| `P`             | Toggle Player ESP              |
| `K`             | Toggle Component Logging       |
| `J`             | Generate Game Structure Log    |

### UI Interface

The mod adds a UI window to the right side of the screen with the following sections:

1.  **Status Toggles**: Enable/disable god mode and various ESP features.
2.  **Action Buttons**:
    - Heal Self / Revive Self
    - Complete Extract
    - Cheap (Make all items cheap)
3.  **Player List**: View other players (Name, Status, HP) with Heal/Revive actions.
4.  **Enemy List**: View active enemies with distance information and kill options.
5.  **Item List**: Track valuable items with distance information. Header shows total value and includes a "Max" button to maximize the closest item's value.

## Logging System

The mod includes a sophisticated logging system that records detailed information about:

- Player components and state changes
- Enemy behavior and properties
- Item values and locations
- World objects and level structure

All logs are stored in: `MelonLoader/Logs/REPO_UTILS/`

### Log Categories

- `Player_Components.log`: Detailed analysis of player objects
- `Enemy_Components.log`: Enemy types and behaviors
- `Item_Components.log`: Item values and properties
- `World_Objects.log`: Level structure and interactive elements
- `Game_Structure.log`: Complete game hierarchy mapping
- `System_Events.log`: Mod status and events

## Development

### Building from Source

1. Clone the repository.
2. Open the solution in Visual Studio 2019/2022.
3. Add references to:
   - MelonLoader.dll
   - Unity Engine assemblies (e.g., `UnityEngine.CoreModule.dll`, `UnityEngine.PhysicsModule.dll` etc.)
   - R.E.P.O game assemblies (e.g., `Assembly-CSharp.dll`).
4. Build the solution.

### Project Structure

- `Core.cs`: Main entry point and manager initialization.
- `PlayerManager.cs`: Player-related functionality, God Mode, player interactions.
- `EnemyManager.cs`: Enemy tracking and manipulation.
- `ItemManager.cs`: Item ESP, value calculation/manipulation, extraction point completion.
- `UIManager.cs`: User interface implementation.
- `LoggingSystem.cs`: Advanced game component analysis.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository.
2. Create a feature branch (`git checkout -b feature/amazing-feature`).
3. Commit your changes (`git commit -m 'Add some amazing feature'`).
4. Push to the branch (`git push origin feature/amazing-feature`).
5. Open a Pull Request.

## Credits

- **Developer**: thultz
- **Testing**: Community contributors
- **Special Thanks**: MelonLoader team for making this possible.

## License

This project is licensed under the MIT License - see the LICENSE file for details. *(Assumes MIT, add LICENSE file if needed)*

## Disclaimer

This mod is for educational purposes only. Use at your own risk. The developers are not responsible for any consequences of using this mod, including but not limited to game bans or multiplayer restrictions.

REPO_UTILS is not affiliated with or endorsed by R.E.P.O or its developers.
