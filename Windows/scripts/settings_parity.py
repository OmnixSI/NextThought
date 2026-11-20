#!/usr/bin/env python3
"""G5: list Mac preference keys and extracted defaults for the parity table."""
from __future__ import annotations
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
TEXT = "\n".join(p.read_text(encoding="utf-8", errors="ignore") for p in (ROOT / "Sources").rglob("*.swift"))
KEYS = sorted(set(re.findall(r'(?:forKey:|object\(forKey:|bool\(forKey:|string\(forKey:|integer\(forKey:|double\(forKey:)\s*"([^"]+)"', TEXT)))
IGNORE = {"AppleLanguages", "CODEX_API_KEY", "OPENAI_API_KEY", "OPENAI_BASE_URL"}
KEYS = [key for key in KEYS if key not in IGNORE]
print(f"extracted={len(KEYS)} expected=113 missing={113 - len(KEYS)}")
for key in KEYS:
    print(key)
