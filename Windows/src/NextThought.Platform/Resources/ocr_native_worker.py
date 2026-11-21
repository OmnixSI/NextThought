"""Compare original Paddle weights, offline and in an isolated process."""
import argparse
import json
import os
import sys
import time
from pathlib import Path
sys.stdout.reconfigure(encoding='utf-8')
os.environ['PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK'] = 'True'
os.environ['HF_HUB_OFFLINE'] = '1'
parser = argparse.ArgumentParser()
parser.add_argument('--image', required=True)
parser.add_argument('--output', required=True)
parser.add_argument('--models', required=True)
args = parser.parse_args()
root = Path(args.models)
import importlib.metadata
for name, version in [('paddlepaddle','3.2.2'),('paddleocr','3.6.0'),('paddlex','3.6.0')]:
    if importlib.metadata.version(name) != version: raise RuntimeError('Unexpected OCR dependency version: '+name)
from paddleocr import PaddleOCRVL
started = time.perf_counter()
pipeline = PaddleOCRVL(pipeline_version='v1.6', layout_detection_model_name='PP-DocLayoutV3',
    layout_detection_model_dir=str(root / 'pp-doclayout-v3'), vl_rec_model_name='PaddleOCR-VL-1.6-0.9B',
    vl_rec_model_dir=str(root / 'paddleocr-vl-1.6-native'), vl_rec_backend='native',
    use_doc_orientation_classify=False, use_doc_unwarping=False, use_chart_recognition=False,
    use_seal_recognition=False, use_queues=False, device='cpu', enable_mkldnn=False, cpu_threads=2)
print('Native Paddle pipeline loaded', flush=True)
results = [{'json': result.json, 'markdown': result.markdown} for result in pipeline.predict(str(Path(args.image).resolve()), max_new_tokens=1024, use_queues=False)]
Path(args.output).write_text(json.dumps({'elapsedSeconds': time.perf_counter()-started, 'results': results}, ensure_ascii=False, default=str, indent=2), encoding='utf-8')
print('Native OCR completed', flush=True)
