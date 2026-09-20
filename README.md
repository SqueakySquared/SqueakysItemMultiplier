# Squeaky's Item Multiplier

It multiplies the items you pick up. It's on the box

## Features

- **Flexible**: 1x to 2,000,000,000x (I've only tested with a few thousand and works)
- **Filtering System**:
  - Configurable lunar item multiplication
  - Configurable void item multiplication
- **Vanilla Friendly**: Only the host needs to install the mod
- **Lightweight Performance**: Optimized server-side processing ensures minimal impact on game performance - until it doesn't


## Temporary Items

Temporary items are not multiplied by default. Enable `MultiplyTemporaryItems` to multiply them while keeping every additional stack temporary so it expires normally.


## Configuration

The configuration file is automatically created at `BepInEx/config/com.squeakysquared.squeakyitemmultiplier.cfg` after the first launch

If [RiskOfOptions](https://thunderstore.io/c/riskofrain2/p/Rune580/Risk_Of_Options/) is installed, these settings are also available in its in-game Mod Options menu. RiskOfOptions is optional; without it, the configuration file continues to work normally.

### Available Settings

| Setting | Default | Range/Options | Description |
|---------|---------|---------------|-------------|
| **ItemMultiplier** | `5` | 1-2B | Controls how many copies of each item you receive |
| **MultiplyLunarItems** | `true` | true/false | Enable or disable multiplication for lunar (blue) items |
| **MultiplyVoidItems** | `true` | true/false | Enable or disable multiplication for void (purple) items |
| **MultiplyTemporaryItems** | `false` | true/false | Multiply temporary items without making the additional stacks permanent |
| **EnableDebugLogging** | `true` | true/false | Provides detailed logging for troubleshooting purposes |


## Compatibility

- **Newest Version**: Fully compatible with the Alloyed Collective update
- **Vanilla Clients**: Seamless integration with vanilla co-op — only the host requires installation
- **Mod Compatibility**: I've tested this with a couple other mods running as well and haven't had problems. Open an issue on the GitHub if you find one


## Important Notes

- Scrap item redemptions are intentionally excluded from multiplication to preserve intended game mechanics - looking into making this an option as well
- World-unique items (such as artifact keys) are not multiplied to maintain progression balance


## Source Code

Available on [GitHub](https://github.com/SqueakySquared/SqueakysItemMultiplier)


## Credits

Developed by Squeaky

Icon by Bahmbu
