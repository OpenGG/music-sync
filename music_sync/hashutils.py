import hashlib
import logging
import re
import subprocess

logger = logging.getLogger(__name__)


def _hash_file_md5(filepath: str) -> str:
    hash_md5 = hashlib.md5()
    with open(filepath, "rb") as f:
        for chunk in iter(lambda: f.read(8192), b""):
            hash_md5.update(chunk)
    return hash_md5.hexdigest()


def _has_only_audio_streams(filepath: str) -> bool:
    try:
        result = subprocess.run(
            [
                "ffprobe",
                "-v",
                "error",
                "-select_streams",
                "v",
                "-show_entries",
                "stream=codec_type",
                "-of",
                "default=nw=1:nk=1",
                filepath,
            ],
            capture_output=True,
            text=True,
            check=True,
            timeout=30,
        )
        return result.stdout.strip() == ""
    except Exception:
        return False


def get_music_md5(filepath: str):
    """Return MD5 hash of the audio content."""
    if _has_only_audio_streams(filepath):
        logger.debug("Hashing %s with hashlib", filepath)
        return _hash_file_md5(filepath)

    logger.debug("Hashing %s with ffmpeg", filepath)
    try:
        result = subprocess.run(
            ["ffmpeg", "-i", filepath, "-vn", "-f", "md5", "-"],
            capture_output=True,
            text=True,
            check=True,
            timeout=60,
        )
        output = (result.stdout or "") + (result.stderr or "")
        match = re.search(r"MD5=([a-f0-9]{32})", output)
        if match:
            return match.group(1)
        logger.warning(
            "Could not find MD5 hash in ffmpeg output for %s. Output: %s",
            filepath,
            output.strip(),
        )  # pragma: no cover - malformed output
        return None
    except FileNotFoundError:  # pragma: no cover - environment issue
        logger.error("ffmpeg not found. Please ensure it's installed and in your PATH.")
        return None
    except subprocess.TimeoutExpired:  # pragma: no cover - slow ffmpeg
        logger.error("ffmpeg timed out for %s", filepath)
        return None
    except subprocess.CalledProcessError as e:  # pragma: no cover - ffmpeg error
        logger.error(
            "Error calculating MD5 for %s: %s. Stderr: %s",
            filepath,
            e.returncode,
            e.stderr.strip() if e.stderr else "",
        )
        return None
    except Exception as e:  # pragma: no cover - unexpected
        logger.error("Unexpected error while getting MD5 for %s: %s", filepath, e)
        return None
