# Squeaky's Item Multiplier

It multiplies the items you pick up. It's on the box

## Features

- **Flexible**: 1x to 100x (could probably do more but haven't tested - also don't get shaped glass if going over 99... or do, idc lol)
- **Filtering System**:
  - Configurable lunar item multiplication
  - Configurable void item multiplication
- **Vanilla Friendly**: Only the host needs to install the mod
- **Lightweight Performance**: Optimized server-side processing ensures minimal impact on game performance - until it doesn't


## Known Issues
- Temp items persist - makes Junk Drones OP
  - If you pick up a junk drone item, it will multiply correctly, however when it expires, only 1 will go away. Looking into making this a configurable option
  - If you use a Temporary Item Dispenser, you'll get [qty] +1 and only the extra will disappear (this is working as inteded, but it does make the math a bit wonky)
      - Artifact of command will multiply each unique item selected. The last item will have the +1 that will be temprorary. All other items will be permanent x[qty]


## Configuration

The configuration file is automatically created at `BepInEx/config/com.squeakysquared.squeakyitemmultiplier.cfg` after the first launch

### Available Settings

| Setting | Default | Range/Options | Description |
|---------|---------|---------------|-------------|
| **ItemMultiplier** | `5` | 1-100 | Controls how many copies of each item you receive |
| **MultiplyLunarItems** | `true` | true/false | Enable or disable multiplication for lunar (blue) items |
| **MultiplyVoidItems** | `true` | true/false | Enable or disable multiplication for void (purple) items |
| **EnableDebugLogging** | `true` | true/false | Provides detailed logging for troubleshooting purposes |


## Compatibility

- **Newest Version**: Fully compatible with the Alloyed Collective update
- **Vanilla Clients**: Seamless integration with vanilla co-op — only the host requires installation
- **Mod Compatibility**: I've tested this with a couple other mods running as well and haven't had problems. Open an issue on the GitHub if you find one


## Important Notes

- Scrap item redemptions are intentionally excluded from multiplication to preserve intended game mechanics - looking into making this an option as well
- World-unique items (such as artifact keys) are not multiplied to maintain progression balance


## Changelog

### v1.0.0
- Works with Risk of Rain 2: Alloyed Collective
- Configurable filtering for lunar and void items


## Source Code

Available on [GitHub](https://github.com/SqueakySquared/SqueakysItemMultiplier)


## Credits

Developed by Squeaky
