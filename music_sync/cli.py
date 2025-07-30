import argparse
import logging
from .core import (
    load_config,
    music_sync,
)


def main() -> None:
    parser = argparse.ArgumentParser(description="Process and organize music files, including DRM decryption.")
    parser.add_argument("-c", "--config", type=str, help="Path to the configuration YAML file.")
    parser.add_argument("-v", "--verbose", action="store_true", help="Enable debug logging")
    args = parser.parse_args()

    logging.basicConfig(level=logging.DEBUG if args.verbose else logging.INFO, format="%(levelname)s: %(message)s")

    cfg = load_config(args.config)
    cfg.music_extensions = [ext.lower() for ext in cfg.music_extensions]

    music_sync(cfg)


if __name__ == "__main__":
    main()
