# RimWorld Access Mod Installer

This Python script automatically installs the RimWorld Access mod and its dependencies.

## Requirements

- Python 3.6 or higher
- Internet connection
- RimWorld installed on your computer

## Dependencies

The script requires the `requests` library. Install it using:

```bash
pip install requests
```

## Usage

1. Open a terminal/command prompt in the mod directory
2. Run the installer:

```bash
python install_rimworld_access.py
```

3. Follow the prompts:
   - The script will automatically find RimWorld if it's in the default Steam location
   - If not found, you'll be asked to enter the path to your RimWorld installation
   - Choose whether you want stable releases or beta/pre-release versions
   - The script will handle the rest automatically

## What the Script Does

1. **Locates RimWorld**: Finds your RimWorld installation directory
2. **Installs Harmony**: Downloads and installs the latest Harmony mod (required dependency)
3. **Installs RimWorld Access**: Downloads and installs the RimWorld Access mod
4. **Configures Mods**: Updates `ModsConfig.xml` to enable the mods in the correct load order

## Load Order

The script ensures mods are loaded in this order:
1. brrainz.harmony (Harmony)
2. ludeon.rimworld (Core game)
3. shane12300.rimworldaccess (RimWorld Access)

## Troubleshooting

- **"Module 'requests' not found"**: Install the requests library with `pip install requests`
- **"RimWorld not found"**: Enter the full path to your RimWorld installation when prompted
- **"ModsConfig.xml not found"**: The file will be created when you first run RimWorld. Enable mods manually in-game the first time.

## Backup

The script automatically creates a backup of your `ModsConfig.xml` file before making changes. The backup is saved as `ModsConfig.xml.backup` in the same directory.

## Manual Installation

If the automatic installer doesn't work, you can install manually:

1. Download HarmonyMod.zip from https://github.com/pardeike/HarmonyRimWorld/releases
2. Download RimWorldAccess.zip from https://github.com/shane12300/rimworld_access/releases
3. Extract both archives and copy the folders to `[RimWorld]/Mods/`
4. Launch RimWorld and enable the mods in the mod menu
