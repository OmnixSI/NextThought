"""Create a content manifest from an already published, optionally signed directory."""
import argparse, datetime, hashlib, json, subprocess
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--directory', type=Path, required=True)
parser.add_argument('--version', required=True)
parser.add_argument('--signing', default='unsigned-preview')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
revision = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
dirty = bool(subprocess.check_output(['git', 'status', '--porcelain', '--untracked-files=no'], cwd=root, text=True).strip())
untracked_inputs = subprocess.check_output(['git', 'ls-files', '--others', '--exclude-standard', '--', 'src', 'scripts', 'installer', 'validation', 'tests',
                                          'README.md', 'P4_VALIDATION.md', 'P4_SECOND_PC.md', 'FEATURE_PARITY.md'], cwd=root, text=True).splitlines()
dirty = dirty or bool(untracked_inputs)
directory = args.directory.resolve()
files = []
for path in sorted(directory.rglob('*')):
    if path.is_symlink():
        raise ValueError('Release directory contains a link')
    if not path.is_file() or path.name == 'release-manifest.json':
        continue
    relative = path.relative_to(directory).as_posix()
    if any(part.lower() in ('sessions', 'models', '.tools', '.cache', '.git') for part in path.relative_to(directory).parts):
        raise ValueError('User data or development files cannot enter a release: ' + relative)
    if path.name.lower() in ('settings.json', 'api-key.bin', 'credentials.bin'):
        raise ValueError('Credentials or personal settings cannot enter a release')
    with path.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    files.append({'path': relative, 'size': path.stat().st_size, 'sha256': digest})
manifest = {'product': 'NextThought.Windows.Preview', 'version': args.version, 'revision': revision,
            'sourceDirty': dirty, 'sourceUntrackedBuildInputs': untracked_inputs, 'archiveSchema': 1, 'signing': args.signing,
            'createdAt': datetime.datetime.now(datetime.timezone.utc).isoformat(),
            'architecture': 'x64', 'minimumWindowsBuild': 19041, 'frameworkDependent': False,
            'modelsIncluded': False, 'enhancedOcrPythonIncluded': False, 'files': files}
(directory / 'release-manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'Manifest: {len(files)} files; version {args.version}; source {revision}; dirty={dirty}; {args.signing}')
