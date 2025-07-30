import os
import sys
import hashlib
import subprocess
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
from music_sync import get_music_md5


def test_get_music_md5_audio_only(tmp_path):
    out = tmp_path / "tone.mp3"
    subprocess.run(
        [
            "ffmpeg",
            "-f",
            "lavfi",
            "-i",
            "anullsrc=r=44100:cl=mono",
            "-t",
            "1",
            "-q:a",
            "9",
            "-acodec",
            "libmp3lame",
            str(out),
            "-y",
        ],
        check=True,
        capture_output=True,
    )
    md5 = get_music_md5(str(out))
    with open(out, "rb") as f:
        expected = hashlib.md5(f.read()).hexdigest()
    assert md5 == expected

