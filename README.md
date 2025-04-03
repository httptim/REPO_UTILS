# REPO_UTILS

A comprehensive MelonLoader utility mod for R.E.P.O that enhances gameplay through ESP features, player modifications, and detailed game information.

![REPO_UTILS](https://i.imgur.com/oeVNQ3x.jpeg)

## Features

### Player Enhancements
- **God Mode**: Become invincible with infinite health and stamina
- **Enhanced Movement**: Increased movement speed and jump capabilities when God Mode is active
- **Player ESP**: Visual tracking for other players in multiplayer sessions
- **Player Management**: Heal, kill, or revive other players in the game

### Enemy Management
- **Enemy ESP**: Visual tracking lines to all enemies in the level
- **Enemy Information**: Real-time distance tracking and status monitoring
- **Enemy Control**: Instantly eliminate enemies from a distance

### Item Features
- **Item ESP**: Visual tracking lines to all valuable items
- **Item Information**: Distance tracking and total value calculation
- **Item Tracking**: Keeps track of all items, even behind walls

### Additional Tools
- **Advanced Logging System**: Monitors game state changes and component values
- **Game Structure Analysis**: Maps the entire game hierarchy for easier modding
- **Streamlined UI**: Toggle-based interface for all features
- **Keybind Support**: Quick access to all functions through customizable keys

## Installation

1. Make sure you have [MelonLoader](https://github.com/LavaGang/MelonLoader) installed (v0.5.7 or higher recommended)
2. Download the latest release of REPO_UTILS from the [Releases](https://github.com/username/REPO_UTILS/releases) page
3. Place the `REPO_UTILS.dll` file in your `R.E.P.O/Mods` folder
4. Launch the game

## Usage

### Key Bindings

| Key | Function |
|-----|----------|
| `;` (Semicolon) | Toggle God Mode |
| `L` | Toggle Enemy ESP |
| `I` | Toggle Item ESP |
| `P` | Toggle Player ESP |
| `K` | Toggle Component Logging |
| `J` | Generate Game Structure Log |

### UI Interface

The mod adds a UI window to the right side of the screen with the following sections:

1. **Status Toggles**: Enable/disable god mode and various ESP features
2. **Player List**: View and manage other players (heal, kill, revive)
3. **Enemy List**: View active enemies with distance information and kill options
4. **Item List**: Track valuable items with distance information and total value

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

1. Clone the repository
2. Open the solution in Visual Studio 2019/2022
3. Add references to:
   - MelonLoader.dll
   - Unity Engine assemblies
   - R.E.P.O game assemblies
4. Build the solution

### Project Structure

- `Core.cs`: Main entry point and manager initialization
- `PlayerManager.cs`: Player-related functionality and god mode
- `EnemyManager.cs`: Enemy tracking and manipulation
- `ItemManager.cs`: Item ESP and value calculation
- `UIManager.cs`: User interface implementation
- `LoggingSystem.cs`: Advanced game component analysis

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## Credits

- **Developer**: thultz
- **Testing**: Community contributors
- **Special Thanks**: MelonLoader team for making this possible

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Disclaimer

This mod is for educational purposes only. Use at your own risk. The developers are not responsible for any consequences of using this mod, including but not limited to game bans or multiplayer restrictions.

REPO_UTILS is not affiliated with or endorsed by R.E.P.O or its developers.
