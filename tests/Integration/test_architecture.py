"""Architecture gates inspect real project references and SQL ownership."""
from pathlib import Path
import re
import unittest
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
MODULES = {'Access', 'Assessments', 'Evaluation'}


class ArchitectureTests(unittest.TestCase):
    def test_module_dependency_direction(self):
        expected = {'Access': set(), 'Assessments': set(), 'Evaluation': {'Assessments'}}
        for module in sorted(MODULES):
            project = ROOT / 'src' / module / f'{module}.csproj'
            references = {Path(node.attrib['Include'].replace('\\', '/')).stem
                          for node in ET.parse(project).iter('ProjectReference')}
            self.assertEqual(expected[module], references, f'{module} dependencies changed')

    def test_module_persistence_is_internal_and_sql_is_owned(self):
        for module in sorted(MODULES):
            for path in (ROOT / 'src' / module).glob('*.cs'):
                source = path.read_text()
                self.assertNotRegex(source, r'public\s+(?:sealed\s+)?class\s+\w+\s*\([^)]*\)\s*:\s*(?:IdentityDbContext|DbContext)', str(path))
                for schema in MODULES - {module}:
                    self.assertNotRegex(source, rf'(?i)\b(?:FROM|JOIN|INTO|UPDATE|TABLE|SCHEMA)\s+"?{schema.lower()}\b', str(path))

    def test_host_is_only_composition_root(self):
        project = ROOT / 'src/Host/Host.csproj'
        references = {Path(node.attrib['Include'].replace('\\', '/')).stem
                      for node in ET.parse(project).iter('ProjectReference')}
        self.assertEqual(MODULES, references)
        for path in (ROOT / 'src/Host').glob('*.cs'):
            self.assertNotRegex(path.read_text(), r'\b(?:DbSet|UseNpgsql|FromSql|ExecuteSql|SaveChanges)\b', str(path))


if __name__ == '__main__':
    unittest.main()
