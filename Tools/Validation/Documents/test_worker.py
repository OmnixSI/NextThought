import unittest,importlib.util,pathlib
spec=importlib.util.spec_from_file_location('worker',pathlib.Path(__file__).resolve().parents[3]/'Resources/DocumentRuntime/worker.py');worker=importlib.util.module_from_spec(spec);spec.loader.exec_module(worker)
class WorkerTests(unittest.TestCase):
 def test_otsl_spans_and_escaping(self):
  out=worker.otsl('<fcel>A<lcel><nl><fcel>B<ucel><nl>')
  self.assertIn('colspan="2"',out);self.assertIn('rowspan="2"',out)
  self.assertIn('&amp;',worker.otsl('<fcel>A&B<nl>'))
 def test_two_columns_read_in_column_order(self):
  regions=[(i,22,box) for i,box in enumerate([(10,10,300,30),(600,10,950,30),(10,50,300,70),(600,50,950,70)])]
  self.assertEqual([x[0] for x in worker.reading_order(regions,1000)],[0,2,1,3])
 def test_spanning_heading_stays_first(self):
  regions=[(0,6,(0,0,1000,30)),(1,22,(10,50,300,70)),(2,22,(600,50,950,70)),(3,22,(10,90,300,110)),(4,22,(600,90,950,110))]
  self.assertEqual([x[0] for x in worker.reading_order(regions,1000)],[0,1,3,2,4])
 def test_nested_table_regions_not_duplicated(self):
  regions=[(0,21,(0,0,1000,1000)),(1,22,(20,20,300,50))]
  self.assertEqual(len(worker.reading_order(regions,1000)),1)
if __name__=='__main__':unittest.main()
