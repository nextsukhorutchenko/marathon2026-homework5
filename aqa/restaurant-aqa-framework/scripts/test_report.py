import tempfile
import unittest
from pathlib import Path
from report import generate


class ReportTests(unittest.TestCase):
    def test_missing_trx_still_writes_a_failed_report(self):
        with tempfile.TemporaryDirectory() as root:
            output = Path(root) / 'index.html'
            self.assertEqual(generate(Path(root), output), 2)
            self.assertIn('No TRX results', output.read_text(encoding='utf-8'))

    def test_reads_namespaced_trx_and_escapes_failure_text(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root)
            (path / 'results.trx').write_text('''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>
              <UnitTestResult testName="owner &amp; guest" outcome="Passed" duration="00:00:01" />
              <UnitTestResult testName="negative" outcome="Failed"><Output><ErrorInfo><Message>&lt;script&gt;bad&lt;/script&gt;</Message></ErrorInfo></Output></UnitTestResult>
              </Results></TestRun>''', encoding='utf-8')
            self.assertEqual(generate(path, path / 'index.html'), 1)
            html = (path / 'index.html').read_text(encoding='utf-8')
            self.assertIn('owner &amp; guest', html)
            self.assertIn('&lt;script&gt;bad&lt;/script&gt;', html)
            self.assertNotIn('<script>', html)
            self.assertIn('Passed: 1', html)
            self.assertIn('Failed: 1', html)

    def test_cleanup_failure_prevents_a_green_report(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root)
            (path / 'results.trx').write_text('<TestRun><UnitTestResult testName="test" outcome="Passed" /></TestRun>')
            (path / 'cleanup-ledger.json').write_text('{"prefix":"aqa-test","state":"cleanup-failed"}')
            self.assertEqual(generate(path, path / 'index.html'), 1)
            self.assertIn('cleanup-failed', (path / 'index.html').read_text(encoding='utf-8'))

    def test_report_includes_inherited_and_case_nunit_categories(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root)
            (path / 'results.trx').write_text('<TestRun><UnitTestResult testName="test" outcome="Passed" /></TestRun>')
            (path / 'nunit.xml').write_text('''<test-run><test-suite><properties><property name="Category" value="Ui" /></properties>
              <test-case name="test"><properties><property name="Category" value="Level3" /></properties></test-case></test-suite></test-run>''')
            self.assertEqual(generate(path, path / 'index.html'), 0)
            self.assertIn('Level3, Ui', (path / 'index.html').read_text(encoding='utf-8'))


if __name__ == '__main__':
    unittest.main()
