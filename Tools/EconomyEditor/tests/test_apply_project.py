import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

import yaml

spec = importlib.util.spec_from_file_location("apply_project", Path(__file__).parents[1] / "scripts/apply_project.py")
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class ApplyProjectTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.path = self.root / "Resources/Prototypes/vendors.yml"
        self.path.parent.mkdir(parents=True)
        self.path.write_text("""# File comment survives.
- type: entity
  id: TestVendor
  parent: IS14VendingMachineBase
  name: test vendor
  components:
  - type: IS14VendingMachine
    buySound: !type:SoundCollectionSpecifier
      collection: Vend
    tabs: []
  # Sprite comment survives.
  - type: Sprite
    sprite: test.rsi

- type: entity
  id: TestItem
  name: test item

- type: accessLevel
  id: Engineering

- type: stationBankAccount
  id: StationEngineering
""", encoding="utf-8")
        self.project = {"schemaVersion": 1, "vendors": [{"id": "TestVendor", "revenueAccount": "StationEngineering", "taxPercent": 0.1,
            "tabs": [{"name": "editor-test-tools", "labelRu": "\u0418\u043d\u0441\u0442\u0440\u0443\u043c\u0435\u043d\u0442\u044b", "labelEn": "Tools", "access": ["Engineering"],
                      "inventory": [{"itemId": "TestItem", "price": 125, "stock": 3}]}], "contrabandInventory": []}]}

    def tearDown(self):
        self.temp.cleanup()

    def test_preview_is_read_only_and_apply_preserves_other_components_and_tags(self):
        original = self.path.read_text(encoding="utf-8")
        changes = module.plan(self.project, self.root)
        self.assertEqual(original, self.path.read_text(encoding="utf-8"))
        module.apply(changes)
        updated = self.path.read_text(encoding="utf-8")
        self.assertIn("!type:SoundCollectionSpecifier", updated)
        self.assertIn("# File comment survives.", updated)
        self.assertIn("# Sprite comment survives.\n  - type: Sprite\n    sprite: test.rsi", updated)
        self.assertIn("price: 125", updated)
        self.assertNotRegex(updated, r"[\u0400-\u04ff]")
        for locale, label in [("ru-RU", self.project["vendors"][0]["tabs"][0]["labelRu"]), ("en-US", "Tools")]:
            self.assertIn(label, (self.root / "Resources/Locale" / locale / "_IS14/economy-editor.ftl").read_text(encoding="utf-8"))
        self.assertEqual({}, module.plan(self.project, self.root))

    def test_unknown_item_aborts_before_writes(self):
        original = self.path.read_bytes()
        self.project["vendors"][0]["tabs"][0]["inventory"][0]["itemId"] = "DeletedItem"
        with self.assertRaisesRegex(ValueError, "Unknown entity"):
            module.plan(self.project, self.root)
        self.assertEqual(original, self.path.read_bytes())

    def test_duplicate_item_is_rejected(self):
        entries = self.project["vendors"][0]["tabs"][0]["inventory"]
        entries.append(entries[0].copy())
        with self.assertRaisesRegex(ValueError, "Duplicate"):
            module.plan(self.project, self.root)

    def test_conflicting_localization_is_rejected(self):
        tab = json.loads(json.dumps(self.project["vendors"][0]["tabs"][0]))
        tab["labelEn"] = "Different label"
        self.project["vendors"][0]["tabs"].append(tab)
        with self.assertRaisesRegex(ValueError, "Conflicting tab"):
            module.plan(self.project, self.root)

    def test_changed_source_does_not_get_overwritten(self):
        changes = module.plan(self.project, self.root)
        self.path.write_text("# Concurrent edit\n", encoding="utf-8")
        with self.assertRaisesRegex(RuntimeError, "File changed"):
            module.apply(changes)
        self.assertEqual("# Concurrent edit\n", self.path.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
