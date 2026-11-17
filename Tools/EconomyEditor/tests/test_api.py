"""Run against a disposable editor database: EDITOR_URL defaults to localhost:5187."""
import copy
import io
import json
import os
import urllib.error
import urllib.request
import unittest
import zipfile

BASE = os.getenv("EDITOR_URL", "http://127.0.0.1:5187")
TOKEN = os.getenv("EDITOR_TEST_TOKEN", "")


def request(path, body=None, method=None):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(BASE + path, data, method=method, headers={"Content-Type": "application/json", "X-Editor-Token": TOKEN})
    try:
        with urllib.request.urlopen(req) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as response:
        with response:
            return response.code, response.read()


class ApiTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        status, raw = request("/api/catalog")
        assert status == 200, raw
        cls.catalog = json.loads(raw)

    def project(self):
        return {"schemaVersion": 1, "id": "", "title": "API regression project", "author": "automated-test", "notes": "", "catalogVersion": self.catalog["version"], "revision": 0,
                "vendors": copy.deepcopy(self.catalog["vendors"])}

    def test_revision_conflict_history_and_export(self):
        p = self.project()
        status, raw = request("/api/projects", p, "POST")
        self.assertEqual(200, status, raw)
        first = json.loads(raw)
        second = copy.deepcopy(first)
        second["notes"] = "Updated"
        status, raw = request("/api/projects/" + first["id"], second, "PUT")
        self.assertEqual(200, status, raw)
        self.assertEqual(2, json.loads(raw)["revision"])
        self.assertEqual(409, request("/api/projects/" + first["id"], first, "PUT")[0])
        _, raw = request("/api/projects/" + first["id"] + "/history")
        self.assertEqual([2, 1], json.loads(raw))
        _, raw = request("/api/projects/" + first["id"] + "/history/1")
        self.assertEqual("", json.loads(raw)["notes"])
        status, raw = request("/api/export", p)
        self.assertEqual(200, status, raw[:300])
        with zipfile.ZipFile(io.BytesIO(raw)) as archive:
            self.assertIn("project.json", archive.namelist())
            self.assertIn("ru-RU.ftl", archive.namelist())
            yaml = archive.read("components/" + p["vendors"][0]["id"] + ".yml").decode()
            self.assertIn("- type: IS14VendingMachine", yaml)
            self.assertNotRegex(yaml, r"[\u0400-\u04ff]")

    def test_invalid_inventory_never_exports(self):
        p = self.project()
        p["vendors"][0]["tabs"][0]["inventory"] = [{"itemId": "MissingEditorTestItem", "price": -5, "stock": 2}]
        self.assertEqual(400, request("/api/export", p)[0])
        self.assertEqual(400, request("/api/projects", p)[0])

    def test_missing_items_can_be_saved_as_drafts_but_not_exported(self):
        p = self.project()
        p["vendors"][0]["tabs"][0]["inventory"] = [{"itemId": "MissingEditorTestItem", "price": 5, "stock": 2}]
        self.assertEqual(200, request("/api/projects", p)[0])
        self.assertEqual(400, request("/api/export", p)[0])

    def test_null_fields_are_validation_errors(self):
        p = self.project()
        p["vendors"][0]["tabs"] = None
        self.assertEqual(400, request("/api/projects", p)[0])

    def test_localization_collisions_are_rejected(self):
        p = self.project()
        tab = copy.deepcopy(p["vendors"][0]["tabs"][0])
        tab["labelEn"] += " different"
        p["vendors"][0]["tabs"].append(tab)
        self.assertEqual(400, request("/api/export", p)[0])


if __name__ == "__main__":
    unittest.main()
