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
replace('Sources/InterviewHacker/LocalWhisperService.swift',
        '08f6827c41e9a67e1f2688f3e4ff808878a7792f8b4dd5479952f759f03a8148',
        '69f1608b9ee4be5b362e89603b1ac69e70a0c24573f4ac5256966e94bb3d0ed9',
        'try await withCheckedThrowingContinuation { continuation in',
        'try await withCheckedThrowingContinuation { (continuation: CheckedContinuation<Void, Error>) in')
replace('Sources/InterviewHacker/LocalWhisperService.swift',
        '69f1608b9ee4be5b362e89603b1ac69e70a0c24573f4ac5256966e94bb3d0ed9',
        '4dc171f8abe68df08adcb914a7fe454bd2d5b4edfe0e277d95e8c36729ec9bec',
        'Thread.sleep(forTimeInterval: 0.05)',
        'try? await Task.sleep(for: .milliseconds(50))')
replace('Sources/InterviewHacker/AudioCaptureService.swift',
        '28eb4ee3f7a70706de4ecad26ff8da188030c409638c8445934d7bc4f44b1d7b',
        '6798fee34bca1ab33697589fa168f7a1cff40ee186b1f2633a210fc2efb43db5',
        '        await withCheckedContinuation { continuation in\n            audioQueue.async { continuation.resume(returning: self.segmenter.flush().map { self.stamped($0) }) }\n        }',
        '        audioQueue.sync { segmenter.flush().map { stamped($0) } }')
