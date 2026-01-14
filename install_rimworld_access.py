"""
RimWorld Access Mod Installer
Automatically installs Harmony and RimWorld Access mods for RimWorld.
"""

import os
import sys
import zipfile
import shutil
import requests
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Optional


def find_rimworld_directory() -> Path:
    """
    Find the RimWorld installation directory.
    Checks default location first, then prompts user if not found.
    """
    default_path = Path(r"C:\Program Files (x86)\Steam\steamapps\common\RimWorld")

    if default_path.exists() and default_path.is_dir():
        print(f"Found RimWorld at: {default_path}")
        return default_path

    print(f"RimWorld not found at default location: {default_path}")
    while True:
        user_path = input("Please enter the path to your RimWorld installation: ").strip('"').strip("'")
        path = Path(user_path)

        if path.exists() and path.is_dir():
            # Verify it's actually a RimWorld directory by checking for key files
            if (path / "RimWorldWin64.exe").exists() or (path / "RimWorldWin.exe").exists():
                print(f"RimWorld installation verified at: {path}")
                return path
            else:
                print("This doesn't appear to be a valid RimWorld installation directory.")
        else:
            print("Directory not found. Please try again.")


def download_file(url: str, destination: Path) -> bool:
    """
    Download a file from a URL to a destination path.
    """
    try:
        print(f"Downloading from {url}...")
        response = requests.get(url, stream=True, allow_redirects=True)
        response.raise_for_status()

        total_size = int(response.headers.get('content-length', 0))
        block_size = 8192
        downloaded = 0

        with open(destination, 'wb') as f:
            for chunk in response.iter_content(chunk_size=block_size):
                if chunk:
                    f.write(chunk)
                    downloaded += len(chunk)
                    if total_size > 0:
                        percent = (downloaded / total_size) * 100
                        print(f"\rProgress: {percent:.1f}%", end='', flush=True)

        print("\nDownload complete!")
        return True
    except Exception as e:
        print(f"\nError downloading file: {e}")
        return False


def get_latest_github_release(repo: str, release_type: str = 'stable') -> Optional[str]:
    """
    Get the download URL for the latest release from a GitHub repository.

    Args:
        repo: Repository in format "owner/repo"
        release_type: Type of release - 'stable', 'beta', or 'dev'
            - 'stable': Latest stable release (e.g., v1.0.0)
            - 'beta': Latest beta release (e.g., v1.0.1-beta)
            - 'dev': Development build (tag: 'dev')

    Returns:
        Download URL for the zip file, or None if not found
    """
    try:
        if release_type == 'stable':
            # Get only the latest stable release (no pre-release tags)
            api_url = f"https://api.github.com/repos/{repo}/releases/latest"
            response = requests.get(api_url)
            response.raise_for_status()
            latest_release = response.json()
        else:
            # Get all releases including pre-releases
            api_url = f"https://api.github.com/repos/{repo}/releases"
            response = requests.get(api_url)
            response.raise_for_status()
            releases = response.json()

            if not releases:
                print(f"No releases found for {repo}")
                return None

            if release_type == 'beta':
                # Filter for beta releases (tag contains '-beta')
                beta_releases = [
                    r for r in releases
                    if '-beta' in r.get('tag_name', '').lower()
                ]
                if not beta_releases:
                    print(f"No beta releases found for {repo}")
                    return None
                latest_release = beta_releases[0]
            elif release_type == 'dev':
                # Find the 'dev' tag specifically
                dev_releases = [
                    r for r in releases
                    if r.get('tag_name', '').lower() == 'dev'
                ]
                if not dev_releases:
                    print(f"No 'dev' tag found for {repo}")
                    return None
                latest_release = dev_releases[0]
            else:
                print(f"Invalid release_type: {release_type}")
                return None

        # Find the zip file in assets
        for asset in latest_release.get('assets', []):
            if asset['name'].endswith('.zip'):
                print(f"Found release: {latest_release.get('tag_name', 'unknown')} - {latest_release.get('name', '')}")
                return asset['browser_download_url']

        print(f"No zip file found in latest release for {repo}")
        return None
    except Exception as e:
        print(f"Error fetching release info from GitHub: {e}")
        return None


def extract_zip(zip_path: Path, extract_to: Path) -> bool:
    """
    Extract a zip file to a destination directory.
    """
    try:
        print(f"Extracting {zip_path.name}...")
        with zipfile.ZipFile(zip_path, 'r') as zip_ref:
            zip_ref.extractall(extract_to)
        print("Extraction complete!")
        return True
    except Exception as e:
        print(f"Error extracting zip file: {e}")
        return False


def install_harmony_mod(rimworld_path: Path) -> bool:
    """
    Download and install the Harmony mod.
    """
    print("\n Installing Harmony Mod")
    mods_dir = rimworld_path / "Mods"
    mods_dir.mkdir(exist_ok=True)

    # Download Harmony
    harmony_url = get_latest_github_release("pardeike/HarmonyRimWorld")
    if not harmony_url:
        print("Failed to get Harmony download URL")
        return False

    temp_zip = Path("HarmonyMod.zip")
    if not download_file(harmony_url, temp_zip):
        return False

    # Extract to temporary directory
    temp_extract = Path("temp_harmony")
    if not extract_zip(temp_zip, temp_extract):
        temp_zip.unlink(missing_ok=True)
        return False

    # Move the extracted folder to Mods directory
    # Find the actual mod folder (should be the only directory in temp_extract)
    extracted_folders = [f for f in temp_extract.iterdir() if f.is_dir()]
    if not extracted_folders:
        print("Error: No folder found in extracted Harmony zip")
        shutil.rmtree(temp_extract, ignore_errors=True)
        temp_zip.unlink(missing_ok=True)
        return False

    harmony_folder = extracted_folders[0]
    destination = mods_dir / harmony_folder.name

    # Remove existing installation if present
    if destination.exists():
        print(f"Removing existing Harmony installation at {destination}")
        shutil.rmtree(destination)

    shutil.move(str(harmony_folder), str(destination))
    print(f"Harmony mod installed to: {destination}")

    # Cleanup
    shutil.rmtree(temp_extract, ignore_errors=True)
    temp_zip.unlink(missing_ok=True)

    return True


def install_rimworld_access_mod(rimworld_path: Path, release_type: str) -> bool:
    """
    Download and install the RimWorld Access mod.

    Args:
        rimworld_path: Path to RimWorld installation
        release_type: Type of release to download ('stable', 'beta', or 'dev')
    """
    print("\n Installing RimWorld Access ")
    mods_dir = rimworld_path / "Mods"
    mods_dir.mkdir(exist_ok=True)

    # Download RimWorld Access
    access_url = get_latest_github_release("shane12300/rimworld_access", release_type=release_type)
    if not access_url:
        print("Failed to get RimWorld Access download URL")
        return False

    temp_zip = Path("RimWorldAccess.zip")
    if not download_file(access_url, temp_zip):
        return False

    # Extract to temporary directory
    temp_extract = Path("temp_access")
    if not extract_zip(temp_zip, temp_extract):
        temp_zip.unlink(missing_ok=True)
        return False

    # Move the extracted folder to Mods directory
    extracted_folders = [f for f in temp_extract.iterdir() if f.is_dir()]
    if not extracted_folders:
        print("Error: No folder found in extracted RimWorld Access zip")
        shutil.rmtree(temp_extract, ignore_errors=True)
        temp_zip.unlink(missing_ok=True)
        return False

    access_folder = extracted_folders[0]
    destination = mods_dir / access_folder.name

    # Remove existing installation if present
    if destination.exists():
        print(f"Removing existing RimWorld Access installation at {destination}")
        shutil.rmtree(destination)

    shutil.move(str(access_folder), str(destination))
    print(f"RimWorld Access mod installed to: {destination}")

    # Cleanup
    shutil.rmtree(temp_extract, ignore_errors=True)
    temp_zip.unlink(missing_ok=True)

    return True


def update_mods_config(rimworld_path: Path) -> bool:
    """
    Update ModsConfig.xml to enable the required mods.
    """
    print("\n Updating ModsConfig.xml ")

    # ModsConfig.xml is typically in the user's config directory
    # For Windows: C:\Users\[Username]\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config
    config_dir = Path.home() / "AppData" / "LocalLow" / "Ludeon Studios" / "RimWorld by Ludeon Studios" / "Config"
    config_file = config_dir / "ModsConfig.xml"

    if not config_file.exists():
        print(f"Warning: ModsConfig.xml not found at {config_file}")
        print("The file will be created when you first run RimWorld.")
        print("Please run the game, and try again.")
        return True

    try:
        # Parse the XML file
        tree = ET.parse(config_file)
        root = tree.getroot()

        # Find or create the activeMods element
        active_mods = root.find('activeMods')
        if active_mods is None:
            active_mods = ET.SubElement(root, 'activeMods')

        # Get current active mods
        current_mods = [li.text for li in active_mods.findall('li') if li.text]

        required_mods = [
            'brrainz.harmony',
            'ludeon.rimworld',
            'shane12300.rimworldaccess'
        ]

        # Check if all required mods are already present in the correct order
        if current_mods[:3] == required_mods:
            print("ModsConfig.xml already has the correct configuration. No changes needed.")
            return True

        # Create backup
        backup_file = config_file.with_suffix('.xml.backup')
        shutil.copy2(config_file, backup_file)
        print(f"Backup created at: {backup_file}")

        # Clear existing activeMods and rebuild
        active_mods.clear()

        # Add required mods first
        for mod_id in required_mods:
            li = ET.SubElement(active_mods, 'li')
            li.text = mod_id

        # Add any other mods that were previously active (but not duplicates)
        for mod_id in current_mods:
            if mod_id not in required_mods:
                li = ET.SubElement(active_mods, 'li')
                li.text = mod_id

        # Write the modified XML back to file
        tree.write(config_file, encoding='utf-8', xml_declaration=True)
        print("ModsConfig.xml updated successfully!")

        return True
    except Exception as e:
        print(f"Error updating ModsConfig.xml: {e}")
        return False


def main():
    """
    Main installation routine.
    """
    print("RimWorld Access Mod Installer")

    try:
        # Step 1: Find RimWorld directory
        rimworld_path = find_rimworld_directory()

        # Step 2: Install Harmony mod
        if not install_harmony_mod(rimworld_path):
            print("\nFailed to install Harmony mod. Installation aborted.")
            return 1

        # Step 3: Ask which version to install
        print("\nWhich version of RimWorld Access do you want to install?")
        print("1. Stable - Latest stable release (recommended)")
        print("2. Beta - Latest beta/pre-release version")
        print("3. Dev - Development build (bleeding edge, may be unstable)")

        while True:
            version_choice = input("\nEnter your choice (1/2/3): ").strip()
            if version_choice == '1':
                release_type = 'stable'
                print("Will download the latest stable release.")
                break
            elif version_choice == '2':
                release_type = 'beta'
                print("Will download the latest beta release.")
                break
            elif version_choice == '3':
                release_type = 'dev'
                print("Will download the development build.")
                break
            else:
                print("Please enter 1, 2, or 3.")

        # Step 4: Install RimWorld Access mod
        if not install_rimworld_access_mod(rimworld_path, release_type):
            print("\nFailed to install RimWorld Access mod. Installation aborted.")
            return 1

        # Step 5: Update ModsConfig.xml
        update_mods_config(rimworld_path)

        print("Installation complete!")
        return 0

    except KeyboardInterrupt:
        print("\n\nInstallation cancelled by user.")
        return 1
    except Exception as e:
        print(f"\n\nUnexpected error: {e}")
        import traceback
        traceback.print_exc()
        return 1


if __name__ == "__main__":
    sys.exit(main())
