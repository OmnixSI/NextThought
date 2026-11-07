"""Update one managed phase block; preserve other phases and user-authored text."""
import argparse
import datetime
import hashlib
import json
import os
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.stdout.reconfigure(encoding='utf-8')
START = '<!-- NEXTTHOUGHT-P1-STATUS:BEGIN -->'
END = '<!-- NEXTTHOUGHT-P1-STATUS:END -->'
P1_STEPS = {'P1-1': '单路与双路采音', 'P1-2': '音频异常处理', 'P1-3': '回答服务联调',
         'P1-4': '纪要联调', 'P1-5': '完整会议验收'}
P2_STEPS = {'P2-1': '基础资料读取', 'P2-2': '关键词检索与引用', 'P2-3': 'Codex 后端',
            'P2-4': '桌面操作与应用采音', 'P2-5': '备份与迁移'}
P3_STEPS = {'P3-0': '评测基线与索引准备', 'P3-1': '语义检索', 'P3-2': '增强 OCR',
            'P3-3': '声纹识别', 'P3-4': '翻译', 'P3-5': '手机伴侣', 'P3-6': '性能优化'}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--document', type=Path, default=Path(r'D:\md Vault\NextThought移植路线.md'))
    parser.add_argument('--step', choices=P1_STEPS | P2_STEPS | P3_STEPS, required=True)
    parser.add_argument('--state', required=True)
    parser.add_argument('--detail', required=True)
    parser.add_argument('--evidence', default='')
    parser.add_argument('--version')
    parser.add_argument('--revision')
    args = parser.parse_args()
    phase = args.step.split('-')[0]
    steps = {'P1': P1_STEPS, 'P2': P2_STEPS, 'P3': P3_STEPS}[phase]
    start = f'<!-- NEXTTHOUGHT-{phase}-STATUS:BEGIN -->'
    end_marker = f'<!-- NEXTTHOUGHT-{phase}-STATUS:END -->'
    artifact = ROOT / 'artifacts' / phase.lower()
    artifact.mkdir(parents=True, exist_ok=True)
    original = args.document.read_bytes()
    digest = hashlib.sha256(original).hexdigest()
    backup = artifact / ('route-backup-' + digest[:16] + '.md')
    if not backup.exists():
        backup.write_bytes(original)
    state_file = artifact / 'progress.json'
    progress = json.loads(state_file.read_text(encoding='utf-8')) if state_file.exists() else {'steps': {}, 'events': []}
    now = datetime.datetime.now().astimezone().isoformat(timespec='seconds')
    event = {'step': args.step, 'state': args.state, 'detail': args.detail, 'evidence': args.evidence, 'updatedAt': now}
    progress['steps'][args.step] = event
    progress['events'].append(event)
    if args.version:
        progress['version'] = args.version
    if args.revision:
        progress['revision'] = args.revision
    intro = ('P0-4 已完成；本轮已复测系统声、HECATE 麦克风、双路采音及 DeepSeek 真实流式请求与取消，证据见下表。' if phase == 'P1' else
             'P0、P1 已完成；本轮推进资料、检索引用、Codex、Windows 桌面操作和备份迁移。HECATE 4 倍麦克风增益沿用，按用户要求不测试 USB 插拔。')
    if phase == 'P3':
        intro = '基于 Windows 0.2.0 开展 P3，先建立固定评测、索引缓存及版本管理，再逐项验证本地增强能力。P1/P2 未验事项仍保留；麦克风实采需用户确认且保留测试弹窗，不测试 USB 插拔。'
    lines = [start, f'## 本轮 {phase} 执行状态', '', f'更新时间：{now}', '', intro,
             '本节为最新执行状态；后续章节保留原路线基线，历史待验收描述以本节更新为准。', '',
             '| 步骤 | 工作 | 最新状态 | 结果 |', '|---|---|---|---|']
    if progress.get('version'):
        executable = (ROOT / ('artifacts/NextThought-win-x64-' + progress['version']) / 'NextThought.exe').as_posix()
        lines[5:5] = [f"当前软件：[NextThought Windows {progress['version']}](<{executable}>)", '']
    if progress.get('revision'):
        lines[5:5] = [f"本轮代码提交：`{progress['revision']}`", '']
    for step, title in steps.items():
        row = progress['steps'].get(step, {'state': '待开始', 'detail': '尚未执行本轮验收'})
        detail = row['detail'].replace('|', '／').replace('\n', ' ')
        lines.append(f"| {step} | {title} | {row['state']} | {detail} |")
    lines += ['', '### 本轮执行记录', '']
    for item in progress['events']:
        lines.append(f"- **{item['updatedAt']} · {item['step']} · {item['state']}**：{item['detail']}")
        if item['evidence']:
            lines.append(f"  - 证据：`{item['evidence']}`")
    lines += ['', end_marker]
    text = original.decode('utf-8-sig')
    newline = '\r\n' if '\r\n' in text else '\n'
    block = newline.join(lines)
    if start in text:
        begin = text.index(start)
        end = text.index(end_marker, begin) + len(end_marker)
        updated = text[:begin] + block + text[end:]
    else:
        position = text.find('<!-- NEXTTHOUGHT-P2-STATUS:BEGIN -->') if phase == 'P3' else text.find(START) if phase == 'P2' else text.find('## 1. 当前进度')
        if position < 0:
            raise ValueError('Cannot locate the route insertion point')
        updated = text[:position] + block + newline * 2 + text[position:]
    if progress.get('version'):
        updated = re.sub(r'(?m)^当前版本：[^\r\n]*', f"当前版本：Windows 预览版 {progress['version']}  ", updated, count=1)
        updated = updated.replace('## 1. 当前进度', '## 1. 0.1.0 原始进度记录', 1)
    if progress.get('revision'):
        updated = re.sub(r'(?m)^当前提交：[^\r\n]*', f"当前提交：`{progress['revision']}`  ", updated, count=1)
    data = (b'\xef\xbb\xbf' if original.startswith(b'\xef\xbb\xbf') else b'') + updated.encode('utf-8')
    temporary = args.document.with_name('.' + args.document.name + '.' + phase.lower() + '.tmp')
    try:
        temporary.write_bytes(data)
        if hashlib.sha256(args.document.read_bytes()).hexdigest() != digest:
            raise RuntimeError('Route changed concurrently; no content was replaced. Re-run after reviewing it.')
        os.replace(temporary, args.document)
        state_file.write_text(json.dumps(progress, ensure_ascii=False, indent=2), encoding='utf-8')
    finally:
        if temporary.exists():
            temporary.unlink()
    print(f'Route updated: {args.step}, {args.state}; original backup: {backup.name}')


if __name__ == '__main__':
    main()
