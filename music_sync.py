#!/usr/bin/env python3

import os
import subprocess
import shutil
import sqlite3
import re
import datetime
import yaml
import tempfile
import argparse

# --- Configuration Loading ---
def load_config(config_path=None):
    """
    Loads configuration from a YAML file.
    Prioritizes:
    1. config_path (if provided)
    2. ./config.yaml (current working directory)
    3. script_dir/config.yaml (directory where the script resides)
    """
    possible_config_paths = []

    # 1. Command line specified path
    if config_path:
        possible_config_paths.append(config_path)

    # 2. Current working directory
    current_working_dir_config = os.path.join(os.getcwd(), 'config.yaml')
    possible_config_paths.append(current_working_dir_config)

    # 3. Script file's directory
    script_dir = os.path.dirname(os.path.abspath(__file__))
    script_dir_config = os.path.join(script_dir, 'config.yaml')
    possible_config_paths.append(script_dir_config)

    found_config_path = None
    for p in possible_config_paths:
        if os.path.exists(p):
            found_config_path = p
            break

    if not found_config_path:
        print("Error: config.yaml not found.")
        print(f"Tried: {', '.join(possible_config_paths)}")
        exit(1)

    print(f"Loading configuration from: {found_config_path}")
    try:
        with open(found_config_path, 'r', encoding='utf-8') as f:
            config = yaml.safe_load(f)
        return config
    except yaml.YAMLError as e:
        print(f"Error parsing config file '{found_config_path}': {e}")
        exit(1)


# --- Global Configuration (loaded after parsing arguments) ---
# These will be populated after initial setup
CONFIG = {}
MUSIC_BAK_DIRS = []
MUSIC_INCOMING_DIR = ''
DB_FILE = 'music_sync.db' # Default database file name
SUPPORTED_music_EXTENSIONS = []


# --- DRM Plugin Loader ---
REGISTERED_DRM_PLUGINS = {}

def load_drm_plugins(plugins_config):
    """
    Dynamically loads DRM decryption Bash script plugins based on configuration.
    Searches for plugin scripts in:
    1. current working directory (e.g., ./ncm_decryptor or ./ncm_decryptor.sh)
    2. script's drm_plugins subdirectory (e.g., __FILE__/drm_plugins/ncm_decryptor or __FILE__/drm_plugins/ncm_decryptor.sh)
    """
    current_working_dir = os.getcwd()
    script_dir = os.path.dirname(os.path.abspath(__file__))
    script_plugin_dir = os.path.join(script_dir, 'drm_plugins')

    for plugin_info in plugins_config:
        if not plugin_info.get('enabled', False):
            continue

        plugin_name = plugin_info.get('name')
        if not plugin_name:
            print("Warning: DRM plugin configured without a 'name'. Skipping.")
            continue

        found_script = None
        # Possible script names (with/without .sh, or other extensions)
        possible_script_filenames = [plugin_name, f"{plugin_name}.sh", f"{plugin_name}.bash"]

        # Search priority 1: Current working directory
        for p_name in possible_script_filenames:
            candidate_path = os.path.join(current_working_dir, p_name)
            if os.path.isfile(candidate_path) and os.access(candidate_path, os.X_OK):
                found_script = candidate_path
                print(f"  Found DRM plugin '{plugin_name}' at current working directory: {found_script}")
                break

        # Search priority 2: Script's drm_plugins subdirectory
        if not found_script:
            for p_name in possible_script_filenames:
                candidate_path = os.path.join(script_plugin_dir, p_name)
                if os.path.isfile(candidate_path) and os.access(candidate_path, os.X_OK):
                    found_script = candidate_path
                    print(f"  Found DRM plugin '{plugin_name}' at script's plugin directory: {found_script}")
                    break

        if not found_script:
            print(f"Warning: DRM plugin script for '{plugin_name}' not found or not executable in expected locations. Skipping.")
            print(f"  Expected locations (examples): '{current_working_dir}/{plugin_name}', '{script_plugin_dir}/{plugin_name}.sh'")
            continue

        for ext in plugin_info.get('extensions', []):
            REGISTERED_DRM_PLUGINS[ext.lower()] = {
                'script_path': found_script
            }
        print(f"Loaded DRM plugin: {plugin_name} (script: {os.path.basename(found_script)}) for extensions {plugin_info.get('extensions')}")

# --- Database Operation Functions ---
def init_db(db_file):
    """Initializes the SQLite database and tables (music_hash and operation_log)."""
    conn = sqlite3.connect(db_file)
    cursor = conn.cursor()
    cursor.execute('''
        CREATE TABLE IF NOT EXISTS music_hash (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            md5_hash TEXT UNIQUE NOT NULL,
            first_processed_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
        )
    ''')
    cursor.execute('''
        CREATE TABLE IF NOT EXISTS operation_log (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            original_path TEXT NOT NULL,
            mtime INTEGER NOT NULL,
            music_md5_hash TEXT,
            result TEXT NOT NULL,
            log_time TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
            UNIQUE (original_path, mtime)
        )
    ''')
    conn.commit()
    conn.close()

def is_music_hash_processed(cursor, md5_hash):
    """Checks if an music MD5 hash is already recorded in the database."""
    cursor.execute("SELECT 1 FROM music_hash WHERE md5_hash = ?", (md5_hash,))
    return cursor.fetchone() is not None

def record_music_hash(cursor, md5_hash):
    """Records a new music MD5 hash into the music_hash table."""
    try:
        cursor.execute("INSERT INTO music_hash (md5_hash) VALUES (?)", (md5_hash,))
        print(f"Recorded new music hash: {md5_hash}")
    except sqlite3.IntegrityError:
        print(f"music hash {md5_hash} already in music_hash table.")

def log_operation(cursor, original_path, mtime, music_md5_hash, result, log_to_db=True):
    """
    Records an operation log.
    If log_to_db is False, it only prints to console, otherwise it writes to the database.
    """
    if log_to_db:
        cursor.execute(
            "INSERT INTO operation_log (original_path, mtime, music_md5_hash, result) VALUES (?, ?, ?, ?)",
            (original_path, mtime, music_md5_hash, result)
        )
        print(f"LOG (DB): {os.path.basename(original_path)} -> Result: {result}")
    else:
        print(f"LOG (Console Only): {os.path.basename(original_path)} -> Result: {result}")

# --- File Processing Helper Functions ---
def get_music_md5(filepath):
    """
    Calculates the MD5 hash of the music content using ffmpeg.
    This method only calculates the hash of the music stream, ignoring video streams (e.g., cover art).
    """
    try:
        result = subprocess.run(
            ["ffmpeg", "-i", filepath, "-vn", "-f", "md5", "-"],
            capture_output=True, text=True, check=True,
            timeout=60
        )
        match = re.search(r'MD5=([a-f0-9]{32})', result.stderr)
        if match:
            return match.group(1)
        else:
            print(f"Warning: Could not find MD5 hash in ffmpeg output for {filepath}. Output: {result.stderr.strip()}")
            return None
    except FileNotFoundError:
        print("Error: ffmpeg not found. Please ensure it's installed and in your PATH.")
        return None
    except subprocess.TimeoutExpired:
        print(f"Error: ffmpeg timed out for {filepath}.")
        return None
    except subprocess.CalledProcessError as e:
        print(f"Error calculating MD5 for {filepath}: {e.returncode}. Stderr: {e.stderr.strip()}")
        return None
    except Exception as e:
        print(f"An unexpected error occurred while getting MD5 for {filepath}: {e}")
        return None

# --- Handles DRM file decryption ---
def _handle_drm_file(full_path, file_mtime, relative_path, name, ext_lower, conn, cursor):
    """
    Handles the decryption and processing of a DRM-protected file.
    The DRM plugin now outputs to a temporary directory, and Python finds the decrypted file.
    """
    plugin_info = REGISTERED_DRM_PLUGINS[ext_lower]
    script_path = plugin_info['script_path']

    target_dir = os.path.join(MUSIC_INCOMING_DIR, os.path.dirname(relative_path))
    decrypted_music_path = None
    temp_dir = None

    print(f"Detecting DRM file ({ext_lower}): {full_path}")

    try:
        temp_dir = tempfile.mkdtemp(prefix="drm_output_")

        print(f"  Calling DRM plugin: {script_path} '{full_path}' '{temp_dir}'")
        result = subprocess.run(
            [script_path, full_path, temp_dir],
            check=True,
            capture_output=True,
            text=True,
            timeout=120
        )
        if result.stdout:
            print(f"  Plugin stdout: {result.stdout.strip()}")
        if result.stderr:
            print(f"  Plugin stderr: {result.stderr.strip()}")

        found_files = []
        for root, _, files in os.walk(temp_dir):
            for filename in files:
                file_ext = os.path.splitext(filename)[1].lower()
                if file_ext in SUPPORTED_music_EXTENSIONS:
                    found_files.append(os.path.join(root, filename))

        if not found_files:
            log_operation(cursor, full_path, file_mtime, None, "dedrm_no_music_found")
            print(f"Error: No supported music file found in temporary output directory {temp_dir} after decryption.")
            return False

        # Prioritize based on SUPPORTED_music_EXTENSIONS order if multiple found
        found_files.sort(key=lambda x: SUPPORTED_music_EXTENSIONS.index(os.path.splitext(x)[1].lower()) if os.path.splitext(x)[1].lower() in SUPPORTED_music_EXTENSIONS else len(SUPPORTED_music_EXTENSIONS))

        decrypted_music_path = found_files[0]

        music_md5_hash = get_music_md5(decrypted_music_path)

        if music_md5_hash is None:
            log_operation(cursor, full_path, file_mtime, None, "md5_fail_dedrm")
            return False

        if is_music_hash_processed(cursor, music_md5_hash):
            log_operation(cursor, full_path, file_mtime, music_md5_hash, "skip_music_hash_exists", log_to_db=False)
            print(f"Skipping already processed music (by hash): {os.path.basename(full_path)}")
            return False

        original_name_base = os.path.splitext(os.path.basename(full_path))[0]
        final_file_ext = os.path.splitext(decrypted_music_path)[1]
        final_target_path = os.path.join(target_dir, original_name_base + final_file_ext)

        if not os.path.exists(target_dir):
            os.makedirs(target_dir)

        shutil.move(decrypted_music_path, final_target_path)

        record_music_hash(cursor, music_md5_hash)
        log_operation(cursor, full_path, file_mtime, music_md5_hash, "dedrm_success")
        print(f"Successfully decrypted and moved {os.path.basename(full_path)} to {final_target_path}")
        return True

    except subprocess.CalledProcessError as e:
        error_msg = e.stderr.strip() if e.stderr else f"Exit code {e.returncode}"
        log_operation(cursor, full_path, file_mtime, None, f"dedrm_fail_plugin_error_{ext_lower}_{e.returncode}")
        print(f"Error calling DRM plugin for {os.path.basename(full_path)}: {error_msg}")
        return False
    except FileNotFoundError:
        log_operation(cursor, full_path, file_mtime, None, f"plugin_script_not_found_{ext_lower}")
        print(f"Error: DRM plugin script '{script_path}' not found or not executable. Check permissions.")
        return False
    except subprocess.TimeoutExpired:
        log_operation(cursor, full_path, file_mtime, None, f"dedrm_timeout_{ext_lower}")
        print(f"Error: DRM plugin timed out for {os.path.basename(full_path)}.")
        return False
    except Exception as e:
        log_operation(cursor, full_path, file_mtime, None, f"dedrm_unexpected_error_{type(e).__name__}")
        print(f"An unexpected error occurred during DRM processing of {os.path.basename(full_path)}: {e}")
        return False
    finally:
        if temp_dir and os.path.exists(temp_dir):
            try:
                shutil.rmtree(temp_dir)
                print(f"  Cleaned up temporary output directory: {temp_dir}")
            except Exception as e:
                print(f"  Warning: Could not clean up temporary directory {temp_dir}: {e}")

# --- Handles regular music file copying ---
def _handle_regular_music_file(full_path, file_mtime, relative_path, name, conn, cursor):
    """Handles the copying and processing of a regular music file."""
    target_dir = os.path.join(MUSIC_INCOMING_DIR, os.path.dirname(relative_path))
    final_target_path = os.path.join(target_dir, os.path.basename(full_path))

    print(f"Detecting music file: {full_path}")
    music_md5_hash = get_music_md5(full_path)

    if music_md5_hash is None:
        log_operation(cursor, full_path, file_mtime, None, "md5_fail_copy")
        return False

    if is_music_hash_processed(cursor, music_md5_hash):
        log_operation(cursor, full_path, file_mtime, music_md5_hash, "skip_music_hash_exists", log_to_db=False)
        print(f"Skipping already processed music (by hash): {os.path.basename(full_path)}")
        return False

    if not os.path.exists(target_dir):
        os.makedirs(target_dir)

    print(f"Copying music file: {os.path.basename(full_path)} to {final_target_path}")
    try:
        shutil.copy2(full_path, final_target_path)
        record_music_hash(cursor, music_md5_hash)
        log_operation(cursor, full_path, file_mtime, music_md5_hash, "copy_success")
        print(f"Successfully copied {os.path.basename(full_path)} to {final_target_path}")
        return True
    except Exception as e:
        log_operation(cursor, full_path, file_mtime, music_md5_hash, f"copy_fail_{type(e).__name__}")
        print(f"Error copying {os.path.basename(full_path)}: {e}")
        return False

# --- Processes a single file ---
def _process_single_file(full_path, source_dir, conn, cursor):
    """
    Processes a single music file, including quick skip checks,
    DRM decryption (via plugin), or regular music file copying.
    """
    file_mtime = int(os.path.getmtime(full_path))
    relative_path = os.path.relpath(full_path, source_dir)

    # --- Quick Skip Check ---
    cursor.execute(
        "SELECT result FROM operation_log WHERE original_path = ? AND mtime = ?",
        (full_path, file_mtime)
    )
    existing_record = cursor.fetchone()

    if existing_record:
        successful_results = ["copy_success", "dedrm_success"]
        if existing_record[0] in successful_results:
            log_operation(cursor, full_path, file_mtime, None, "skip_path_mtime_exists", log_to_db=False)
            conn.commit()
            return

        print(f"File {os.path.basename(full_path)} was previously processed with result '{existing_record[0]}'. Retrying.")

    # --- Normal Processing Flow ---
    name, ext = os.path.splitext(os.path.basename(full_path))
    ext_lower = ext.lower()

    if ext_lower in REGISTERED_DRM_PLUGINS:
        _handle_drm_file(full_path, file_mtime, relative_path, name, ext_lower, conn, cursor)
    elif ext_lower in SUPPORTED_music_EXTENSIONS:
        _handle_regular_music_file(full_path, file_mtime, relative_path, name, conn, cursor)
    else:
        log_operation(cursor, full_path, file_mtime, None, "unsupported_type")
        print(f"Skipping unsupported file type: {full_path}")

    conn.commit()


# --- Main Processing Function ---
def music_sync():
    """
    Main function to process all music files from configured source directories.
    Handles DRM decryption via Bash script plugins, duplicates using music hash,
    and skips already processed files based on path and mtime.
    """
    load_drm_plugins(CONFIG.get('drm_plugins', []))

    init_db(DB_FILE)

    if not os.path.exists(MUSIC_INCOMING_DIR):
        os.makedirs(MUSIC_INCOMING_DIR)

    conn = sqlite3.connect(DB_FILE)
    cursor = conn.cursor()

    try:
        for source_dir in MUSIC_BAK_DIRS:
            if not os.path.exists(source_dir):
                print(f"Warning: Source directory not found: {source_dir}. Skipping.")
                continue

            print(f"\n--- Processing files from: {source_dir} ---")
            for root, _, files in os.walk(source_dir):
                for filename in files:
                    full_path = os.path.join(root, filename)
                    _process_single_file(full_path, source_dir, conn, cursor)

    finally:
        conn.close()
        print("\n--- Music synchronization complete ---")

if __name__ == "__main__":
    # Parse command line arguments
    parser = argparse.ArgumentParser(description="Process and organize music files, including DRM decryption.")
    parser.add_argument('-c', '--config', type=str,
                        help="Path to the configuration YAML file.")
    args = parser.parse_args()

    # Load configuration based on the new priority logic
    GLOBAL_CONFIG = load_config(args.config)

    # Populate global configuration variables
    CONFIG.update(GLOBAL_CONFIG)
    MUSIC_BAK_DIRS = CONFIG.get('music_sources', [])
    MUSIC_INCOMING_DIR = CONFIG.get('music_incoming_dir', 'music_incoming')
    DB_FILE = CONFIG.get('database_file', DB_FILE)
    SUPPORTED_music_EXTENSIONS = [ext.lower() for ext in CONFIG.get('music_extensions', [])]

    # Start the main processing
    music_sync()
