"""Run with py -3.12. Creates only NextThought's optional isolated CPU environment."""
import os
from pathlib import Path
import subprocess
import sys

if sys.version_info[:2] != (3, 12) or sys.maxsize <= 2**32:
    raise SystemExit('Requires 64-bit Python 3.12; validation used 3.12.10. No global packages are changed.')
scripts = Path(__file__).resolve().parent
root = Path(os.environ['LOCALAPPDATA']) / 'NextThought'
runtime = root / 'runtimes/paddleocr-vl-cpu'
python = runtime / 'Scripts/python.exe'
if not python.exists(): subprocess.run([sys.executable, '-m', 'venv', str(runtime)], check=True)
lock = scripts / 'ocr-requirements-lock.txt'
if not lock.exists(): lock = scripts.parent / 'validation/p3/ocr-requirements-lock.txt'
subprocess.run([str(python), '-m', 'pip', 'install', '--disable-pip-version-check', 'pip==26.3'], check=True)
subprocess.run([str(python), '-m', 'pip', 'install', '--no-compile', '--no-cache-dir', '--timeout', '120', '--retries', '5', '--resume-retries', '10', '-r', str(lock)], check=True)
subprocess.run([str(python), '-m', 'pip', 'check'], check=True)
for helper in ['prepare_native_ocr_model.py', 'prepare_layout_model.py']:
    subprocess.run([sys.executable, str(scripts / helper)], check=True)
print('Optional OCR environment prepared; CPU page processing can take several minutes. Verify from the material library.')
