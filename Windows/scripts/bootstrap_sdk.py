"""Install a verified, project-local .NET SDK; never changes the machine PATH."""
import hashlib
import json
import pathlib
import time
import urllib.request
import zipfile

ROOT = pathlib.Path(__file__).resolve().parents[1]
CACHE = ROOT / '.cache'
DESTINATION = ROOT / '.tools' / 'dotnet'


def main():
    CACHE.mkdir(exist_ok=True)
    metadata = json.loads((ROOT / 'scripts' / 'sdk-manifest.json').read_text(encoding='utf-8'))
    if (DESTINATION / 'sdk' / metadata['version']).is_dir():
        print('SDK already present:', metadata['version'], flush=True)
        return
    asset = metadata['asset']
    archive = CACHE / ('dotnet-sdk-' + metadata['version'] + '-win-x64.zip')
    expected = asset['hash'].lower()
    if not archive.exists() or hashlib.file_digest(archive.open('rb'), 'sha512').hexdigest() != expected:
        for attempt in range(6):
            try:
                print('Downloading Microsoft .NET SDK', metadata['version'], flush=True)
                with urllib.request.urlopen(asset['url'], timeout=60) as response, archive.open('wb') as target:
                    count = 0
                    last = 0
                    while block := response.read(1024 * 1024):
                        target.write(block)
                        count += len(block)
                        if count-last > 25*1024*1024:
                            print('SDK downloaded:', count//1024//1024, 'MiB', flush=True)
                            last = count
                with archive.open('rb') as source:
                    actual = hashlib.file_digest(source, 'sha512').hexdigest()
                if actual != expected:
                    raise ValueError('SDK SHA512 mismatch')
                break
            except (OSError, ValueError) as error:
                print('Download retry:', attempt+1, type(error).__name__, flush=True)
                if attempt == 5:
                    raise
                time.sleep(2)
    DESTINATION.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as package:
        for item in package.infolist():
            output = (DESTINATION / item.filename).resolve()
            output.relative_to(DESTINATION.resolve())
        package.extractall(DESTINATION)
    print('Verified SDK installed:', DESTINATION, flush=True)


if __name__ == '__main__':
    main()
