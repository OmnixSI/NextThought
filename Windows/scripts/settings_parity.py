#!/usr/bin/env python3
"""G5: list the 113 Mac preference keys and extracted defaults for the parity table."""
from __future__ import annotations
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCES = "\n".join(p.read_text(encoding="utf-8", errors="ignore") for p in (ROOT / "Sources").rglob("*.swift"))
KEYS = set(re.findall(r'(?:forKey:|object\(forKey:|bool\(forKey:|string\(forKey:|integer\(forKey:|double\(forKey:)\s*"([^"]+)"', SOURCES))
KEYS.update(re.findall(r'@AppStorage\("([^"]+)"', SOURCES))
KEYS.update(re.findall(r'static let key = "([^"]+)"', SOURCES))
BACKUP = (ROOT / "Sources/NextThoughtCore/Backup.swift").read_text(encoding="utf-8")
KEYS.update(re.findall(r'"([^"]+)"', re.search(r"exportedKeys[^=]*=\s*\[(.*?)\]", BACKUP, re.S).group(1)))
KEYS -= {"CODEX_API_KEY", "OPENAI_API_KEY", "OPENAI_BASE_URL"}
KEYS = sorted(KEYS)
print(f"extracted={len(KEYS)} expected=113 missing={113 - len(KEYS)}")
for key in KEYS:
    print(key)
