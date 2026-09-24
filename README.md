# Squeaky's Item Multiplier

It multiplies the items you pick up. It's on the box

## Features

- **Flexible**: 1x to 2,000,000,000x (I've only tested with a few thousand and works)
- **Filtering System**:
  - Configurable lunar item multiplication
  - Configurable void item multiplication
- **Exponential Growth**: Optional increasing grants for repeated pickups of the same item
- **Vanilla Friendly**: Only the host needs to install the mod
- **Lightweight Performance**: Optimized server-side processing ensures minimal impact on game performance - until it doesn't


## Temporary Items

Temporary items are not multiplied by default. Enable `MultiplyTemporaryItems` to multiply them while keeping every additional stack temporary so it expires normally.


## Configuration

The configuration file is automatically created at `BepInEx/config/com.squeakysquared.squeakyitemmultiplier.cfg` after the first launch

Launch the game once with the mod enabled, then close it before editing the `.cfg` file. In r2modman or Thunderstore Mod Manager, use the active profile's Config Editor or browse that profile's folder to find `BepInEx/config`. Save your changes and launch the game again. Editing this README does not change the mod's settings.

If [RiskOfOptions](https://thunderstore.io/c/riskofrain2/p/Rune580/Risk_Of_Options/) is installed, these settings are also available in its in-game Mod Options menu. RiskOfOptions is optional; without it, the configuration file continues to work normally.

### Available Settings

| Setting | Default | Range/Options | Description |
|---------|---------|---------------|-------------|
| **ItemMultiplier** | `5` | 1-2B | Controls how many copies of each item you receive |
| **ExponentialItems** | `false` | true/false | Increase grants exponentially for successive eligible pickups of each item type, per player |
| **MultiplyLunarItems** | `true` | true/false | Enable or disable multiplication for lunar (blue) items |
| **MultiplyVoidItems** | `true` | true/false | Enable or disable multiplication for void (purple) items |
| **MultiplyTemporaryItems** | `false` | true/false | Multiply temporary items without making the additional stacks permanent |
| **EnableDebugLogging** | `true` | true/false | Provides detailed logging for troubleshooting purposes |

### Exponential Items

Enable `ExponentialItems` to use `ItemMultiplier` as the growth base. At `5`, successive pickups of the same item grant **5, 25, 125, 625...** copies (inventory totals of **5, 30, 155, 780...**). Each player and item type has a separate sequence that resets when a run starts or ends. Losing or scrapping items does not rewind the sequence.

Only eligible, positive grants while exponential mode is enabled and the multiplier is above 1 advance the sequence. Turning the option off pauses it; turning it back on resumes it. Changing `ItemMultiplier` uses the new base at the current position. A grant containing several copies scales the whole batch and advances once. Opted-in temporary pickups share their item's sequence and remain temporary.

Grants are capped by remaining inventory capacity; at the limit a pickup may add no copies. Temporary grants are rounded down when needed to stay within the numeric limit.


## Compatibility

- **Newest Version**: Fully compatible with the Alloyed Collective update
- **Vanilla Clients**: Seamless integration with vanilla co-op — only the host requires installation
- **Mod Compatibility**: I've tested this with a couple other mods running as well and haven't had problems. Open an issue on the GitHub if you find one


## Important Notes

- Scrap item redemptions are intentionally excluded from multiplication to preserve intended game mechanics - looking into making this an option as well
- World-unique items (such as artifact keys) are not multiplied to maintain progression balance


## Source Code

Available on [GitHub](https://github.com/SqueakySquared/SqueakysItemMultiplier)

Build with `dotnet build SqueakyItemMultiplier.slnx -c Release`. If needed, pass `-p:RoR2InstallDir="path/to/Risk of Rain 2"` to locate the game's assemblies. Run the standalone progression and numeric-limit checks with `dotnet run --project tests/GrantScaling.Tests -c Release` (.NET 10 SDK).


## Thunderstore Downloads

Thank you for [14.6K+ downloads on Thunderstore](https://thunderstore.io/c/riskofrain2/p/SqueakySquad/Squeakys_Item_Multiplier/)!

Your support means a lot. Happy multiplying!


## Credits

Developed by Squeaky

Icon by Bahmbu
