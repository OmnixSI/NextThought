"""Check an actual image-only PDF import, its original bytes, table cells and locators."""
import argparse
import hashlib
import json
import re
import unicodedata
from html.parser import HTMLParser
from pathlib import Path

class Table(HTMLParser):
    def __init__(self):
        super().__init__(); self.rows = []; self.cell = None
    def handle_starttag(self, tag, attrs):
        if tag == 'tr': self.rows.append([])
        if tag in ('td', 'th'): self.cell = ''
    def handle_data(self, data):
        if self.cell is not None: self.cell += data
    def handle_endtag(self, tag):
        if tag in ('td', 'th') and self.cell is not None:
            self.rows[-1].append(re.sub(r'\s', '', unicodedata.normalize('NFKC', self.cell)))
            self.cell = None

parser = argparse.ArgumentParser()
parser.add_argument('--report', type=Path, required=True)
parser.add_argument('--fixture', type=Path, required=True)
parser.add_argument('--data-root', type=Path, required=True)
parser.add_argument('--benchmark', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
result = json.loads(args.report.read_text('utf-8'))
assert result['passed'] and not result['stagingRemains']
document = result['document']
stored = args.data_root / 'Materials' / document['id'] / ('source' + document['extension'])
digest = hashlib.sha256(args.fixture.read_bytes()).hexdigest()
assert digest == hashlib.sha256(stored.read_bytes()).hexdigest() == document['sha256']
table = next(b for b in document['blocks'] if b['kind'] == 'paddle-table')
parsed = Table(); parsed.feed(table['text'])
assert parsed.rows == [['检测项目', '周期(天)'], ['桥面巡检', '7'], ['支座复核', '90']], parsed.rows
assert all(b['page'] == 1 and len(b['box']) == 4 and all(v >= 0 for v in b['box']) for b in document['blocks'])
cases = [{'id': 'ocr-pdf-' + str(i), 'query': question, 'category': 'ocr-table', 'split': 'synthetic-smoke',
          'relevant': [document['id'] + ':' + table['location']]}
         for i, question in enumerate(['支座复核周期是多少天', '桥面巡检每隔多久'])]
args.benchmark.write_text(json.dumps({'documents': [document], 'cases': cases}, ensure_ascii=False, indent=2), encoding='utf-8')
args.output.write_text(json.dumps({'passed': True, 'originalSha256': digest, 'page': 1,
    'tableRows': parsed.rows, 'blockLocation': table['location'], 'blockCount': len(document['blocks']),
    'importSeconds': result['elapsedSeconds']}, ensure_ascii=False, indent=2), encoding='utf-8')
print('Scanned PDF original hash / table cells / page and block locators: PASS')
