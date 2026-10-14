"""Temporary, exact-hash-checked native compiler corrections; removed on publish."""
import hashlib
from pathlib import Path

def replace(path, before_hash, after_hash, old, new):
    p = Path(path)
    assert path.startswith('Sources/') and '..' not in p.parts and not p.is_symlink()
    data = p.read_bytes()
    assert hashlib.sha256(data).hexdigest() == before_hash, path
    text = data.decode('utf-8')
    assert text.count(old) == 1, path
    result = text.replace(old, new, 1).encode('utf-8')
    assert hashlib.sha256(result).hexdigest() == after_hash, path
    p.write_bytes(result)

replace('Sources/InterviewHackerCore/QuestionDetector.swift',
        '50b1e595b5b4c6f9139ce0ba1eb119fca1a8201863fa7ffe4a2240ba8935e2e9',
        'cb13d40903d3aae276f81bf96024fc04ea1c732d453095e58b4abb29b4a1669b',
        '        recentKeys.removeAll { now.timeIntervalSince($0.date) >= duplicateWindow }',
        '        let duplicateCutoff = now.addingTimeInterval(-duplicateWindow)\n        recentKeys.removeAll { $0.date <= duplicateCutoff }')
