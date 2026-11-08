"""Local-only feasibility runner for the complete Paddle layout/crop/VLM pipeline."""
import argparse
import base64
import json
import os
import secrets
import socket
import subprocess
import sys
import time
import urllib.request
from pathlib import Path

sys.stdout.reconfigure(encoding='utf-8')
os.environ['PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK'] = 'True'
os.environ['HF_HUB_OFFLINE'] = '1'
os.environ['PADDLE_PDX_MODEL_SOURCE'] = 'HF'
parser = argparse.ArgumentParser()
parser.add_argument('--image', required=True)
parser.add_argument('--output', required=True)
parser.add_argument('--direct', action='store_true')
parser.add_argument('--runtime', default='llama-b12189-verified')
args = parser.parse_args()
root = Path(os.environ['LOCALAPPDATA']) / 'NextThought'
model = root / 'models/paddleocr-vl-1.6-gguf'
runtime = model / args.runtime
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0)); port = sock.getsockname()[1]
key = secrets.token_urlsafe(32)
log = Path(args.output).with_suffix('.server.log')
with log.open('w', encoding='utf-8') as stream:
    server = subprocess.Popen([str(runtime / 'llama-server.exe'), '-m', str(model / 'PaddleOCR-VL-1.6-GGUF.gguf'), '--mmproj', str(model / 'PaddleOCR-VL-1.6-GGUF-mmproj.gguf'),
        '--ctx-size', '8192', '--threads', '4', '--threads-batch', '4', '--parallel', '1', '--no-mmproj-offload', '--n-gpu-layers', '0', '--host', '127.0.0.1', '--port', str(port), '--no-webui', '--api-key', key, '--alias', 'PaddleOCR-VL-1.6'],
        stdout=stream, stderr=subprocess.STDOUT, cwd=runtime, creationflags=subprocess.CREATE_NO_WINDOW)
    try:
        started = time.perf_counter()
        while time.perf_counter() - started < 120:
            if server.poll() is not None: raise RuntimeError('Local OCR server exited; see server log')
            try:
                request = urllib.request.Request(f'http://127.0.0.1:{port}/health', headers={'Authorization': 'Bearer ' + key})
                with urllib.request.urlopen(request, timeout=1) as response:
                    if response.status == 200: break
            except Exception: time.sleep(.3)
        else: raise TimeoutError('OCR server startup timeout')
        print('Local VLM ready', flush=True)
        if args.direct:
            payload = {'model': 'PaddleOCR-VL-1.6', 'messages': [{'role': 'user', 'content': [{'type': 'image_url', 'image_url': {'url': 'data:image/png;base64,' + base64.b64encode(Path(args.image).read_bytes()).decode('ascii')}}, {'type': 'text', 'text': 'Table Recognition:'}]}], 'temperature': 0, 'max_tokens': 256}
            request = urllib.request.Request(f'http://127.0.0.1:{port}/v1/chat/completions', data=json.dumps(payload).encode('utf-8'), headers={'Authorization': 'Bearer ' + key, 'Content-Type': 'application/json'})
            with urllib.request.urlopen(request, timeout=120) as response: output = json.load(response)
            Path(args.output).write_text(json.dumps(output, ensure_ascii=False, indent=2), encoding='utf-8')
            print('Direct VLM probe completed', flush=True)
            sys.exit(0)
        from paddleocr import PaddleOCRVL
        pipeline = PaddleOCRVL(pipeline_version='v1.6', layout_detection_model_name='PP-DocLayoutV3', layout_detection_model_dir=str(root / 'models/pp-doclayout-v3'),
            vl_rec_model_name='PaddleOCR-VL-1.6-0.9B', vl_rec_backend='llama-cpp-server', vl_rec_server_url=f'http://127.0.0.1:{port}/v1', vl_rec_api_model_name='PaddleOCR-VL-1.6', vl_rec_api_key=key,
            vl_rec_max_concurrency=1, use_doc_orientation_classify=False, use_doc_unwarping=False, use_chart_recognition=False, use_seal_recognition=False, use_queues=False, device='cpu', enable_mkldnn=False, cpu_threads=2)
        print('Layout pipeline ready', flush=True)
        results = []
        for result in pipeline.predict(str(Path(args.image).resolve()), max_new_tokens=1536, use_queues=False):
            results.append({'json': result.json, 'markdown': result.markdown})
        Path(args.output).write_text(json.dumps({'elapsedSeconds': time.perf_counter() - started, 'results': results}, ensure_ascii=False, default=str, indent=2), encoding='utf-8')
        print('OCR pilot completed', flush=True)
    finally:
        server.terminate()
        try: server.wait(timeout=10)
        except subprocess.TimeoutExpired: server.kill(); server.wait()
