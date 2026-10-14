"""Convert a local OpenAI checkpoint using the user's existing Whisper installation.
No weights or Python dependencies are downloaded; the source file is never modified.
"""
import hashlib
from pathlib import Path
import runpy
import sys


def main():
    source, output, converter = map(Path, sys.argv[1:])
    import whisper
    import torch

    name = source.stem
    if name in whisper._MODELS:
        expected = whisper._MODELS[name].split('/')[-2]
        with source.open('rb') as handle:
            actual = hashlib.file_digest(handle, 'sha256').hexdigest()
        if actual != expected:
            raise ValueError(f'{source.name} 不完整或校验失败，请选择其他完整的本地模型。')
    # Refuse pickle code execution, including custom checkpoints.
    checkpoint = torch.load(source, map_location='cpu', weights_only=True)
    if not isinstance(checkpoint, dict) or not {'dims', 'model_state_dict'} <= checkpoint.keys():
        raise ValueError('请选择 OpenAI Whisper 的完整 .pt 模型。')
    del checkpoint
    original_load = torch.load

    def safe_load(*args, **kwargs):
        kwargs['weights_only'] = True
        return original_load(*args, **kwargs)

    torch.load = safe_load
    output.mkdir(parents=True, exist_ok=True)
    sys.argv = [str(converter), str(source), str(Path(whisper.__file__).parent.parent), str(output)]
    runpy.run_path(str(converter), run_name='__main__')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print(f'导入失败：{error}', file=sys.stderr)
        sys.exit(1)
