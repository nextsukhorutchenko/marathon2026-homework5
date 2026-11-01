"""TRX -> standalone HTML, using only Python's standard library. Non-zero on failure."""
import argparse
import html
import json
import xml.etree.ElementTree as ET
from pathlib import Path


def generate(root: Path, output: Path) -> int:
    results, problems, ledgers = [], [], []
    categories = {}

    def collect(node, inherited):
        own = {prop.get('value', '') for prop in node.findall('./properties/property')
               if prop.get('name') == 'Category'}
        current = inherited | own
        if node.tag == 'test-case':
            categories[node.get('name')] = ', '.join(sorted(current))
        for child in node:
            collect(child, current)

    for nunit in root.rglob('*.xml'):
        try:
            document = ET.parse(nunit)
            if document.getroot().tag == 'test-run':
                collect(document.getroot(), set())
        except (ET.ParseError, OSError):
            problems.append('Cannot read NUnit categories: ' + nunit.name)
    for trx in sorted(root.rglob('*.trx')):
        try:
            document = ET.parse(trx)
            for node in document.iter():
                if node.tag.split('}')[-1] == 'UnitTestResult':
                    errors = '\n'.join(child.text or '' for child in node.iter()
                                       if child.tag.split('}')[-1] in ('Message', 'StackTrace'))
                    results.append((node.get('testName', '?'), node.get('outcome', '?'),
                                    node.get('duration', ''), errors))
                if node.tag.split('}')[-1] == 'ResultSummary' and node.get('outcome') in ('Failed', 'Error', 'Aborted'):
                    problems.append('TRX run failed: ' + trx.name)
        except (ET.ParseError, OSError) as error:
            problems.append('Cannot read TRX: ' + trx.name + ' (' + type(error).__name__ + ')')
    if not results:
        problems.append('No TRX results: build/setup/test discovery may have failed. This is not a passing run.')
    for path in sorted(root.rglob('cleanup-ledger.json')):
        try:
            data = json.loads(path.read_text(encoding='utf-8'))
            ledgers.append((data['prefix'], data['state']))
            if data['state'] != 'cleaned-and-verified':
                problems.append('Cleanup incomplete for ' + data['prefix'] + ': ' + data['state'])
        except (OSError, ValueError, KeyError):
            problems.append('Cannot read cleanup ledger: ' + path.parent.name)
    passed = sum(row[1] == 'Passed' for row in results)
    failed = sum(row[1] == 'Failed' for row in results)
    other = len(results) - passed - failed
    escape = html.escape
    rows = ''.join('<tr><td>' + escape(name) + '</td><td>' + escape(categories.get(name, 'Unavailable (TRX only)')) + '</td><td>' + escape(outcome) + '</td><td>' + escape(duration)
                   + '</td><td><pre>' + escape(error) + '</pre></td></tr>'
                   for name, outcome, duration, error in results)
    states = ''.join('<li>' + escape(prefix + ': ' + state) + '</li>' for prefix, state in ledgers)
    alerts = ''.join('<li>' + escape(problem) + '</li>' for problem in problems)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text('''<!doctype html><html lang="en"><meta charset="utf-8"><title>Restaurant AQA results</title>
<style>body{font:16px system-ui;margin:2rem;color:#172b28}table{border-collapse:collapse;width:100%}td,th{border:1px solid #bbb;padding:.6rem;text-align:left;vertical-align:top}pre{white-space:pre-wrap;overflow-wrap:anywhere;max-width:70ch}li{margin:.4rem}h1{color:#145440}</style>
<h1>Restaurant AQA results</h1><p>Passed: ''' + str(passed) + ' · Failed: ' + str(failed) + ' · Other/skipped: ' + str(other)
                      + '</p><ul>' + alerts + '</ul><table><thead><tr><th>Test</th><th>Categories</th><th>Outcome</th><th>Duration</th><th>Details</th></tr></thead><tbody>'
                      + rows + '</tbody></table><h2>Fixture cleanup</h2><ul>' + states
                      + '</ul><p>Infrastructure results alone do not verify the live API or UI. Failure screenshots and sanitized trace.zip files are stored beside the TRX files.</p></html>', encoding='utf-8')
    return 2 if not results else int(bool(problems or failed or other))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('results', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    raise SystemExit(generate(args.results, args.output or args.results / 'index.html'))
