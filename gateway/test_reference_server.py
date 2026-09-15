import json
import threading
import unittest
import urllib.error
import urllib.request
from http.server import ThreadingHTTPServer

from reference_server import Handler, RouteError, build_plan, parse_route


class RouteTests(unittest.TestCase):
    def test_valid_route(self):
        route = parse_route("/v1/plan/default/quiet_company/warm/quiet/zh/2")
        self.assertEqual(route.intent, "quiet_company")
        self.assertEqual(route.turn_slot, 2)

    def test_invalid_intent_rejected(self):
        with self.assertRaises(RouteError):
            parse_route("/v1/plan/default/arbitrary_text/warm/quiet/zh/2")

    def test_unknown_language_rejected(self):
        with self.assertRaises(RouteError):
            parse_route("/v1/plan/default/greet/new/neutral/fr/0")

    def test_query_parameters_rejected(self):
        with self.assertRaises(RouteError):
            parse_route("/v1/plan/default/greet/new/neutral/en/0?player=secret")

    def test_slot_bounds(self):
        with self.assertRaises(RouteError):
            parse_route("/v1/plan/default/greet/new/neutral/en/4")

    def test_plan_has_behavior_contract_shape(self):
        plan = build_plan(
            parse_route("/v1/plan/default/walk_with_me/familiar/neutral/en/0")
        )
        self.assertEqual(plan["schema_version"], "0.1")
        self.assertTrue(plan["request_id"])
        self.assertEqual(plan["behavior"]["action"], "follow")
        self.assertLessEqual(plan["behavior"]["target_distance_m"], 5.0)


class HttpTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        cls.thread = threading.Thread(target=cls.server.serve_forever, daemon=True)
        cls.thread.start()
        cls.base = f"http://127.0.0.1:{cls.server.server_address[1]}"

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.server.server_close()
        cls.thread.join(timeout=2)

    def test_get_returns_json_and_no_store(self):
        url = self.base + "/v1/plan/default/greet/new/neutral/en/0"
        with urllib.request.urlopen(url, timeout=2) as response:
            self.assertEqual(response.status, 200)
            self.assertEqual(response.headers["Cache-Control"], "no-store")
            payload = json.loads(response.read())
        self.assertEqual(payload["behavior"]["action"], "wave")

    def test_bad_route_is_bounded_json_error(self):
        url = self.base + "/v1/plan/default/not_real/new/neutral/en/0"
        with self.assertRaises(urllib.error.HTTPError) as ctx:
            urllib.request.urlopen(url, timeout=2)
        self.assertEqual(ctx.exception.code, 400)
        payload = json.loads(ctx.exception.read())
        self.assertEqual(payload["error"], "invalid_route")

    def test_post_is_rejected(self):
        request = urllib.request.Request(
            self.base + "/v1/plan/default/greet/new/neutral/en/0",
            data=b"{}",
            method="POST",
        )
        with self.assertRaises(urllib.error.HTTPError) as ctx:
            urllib.request.urlopen(request, timeout=2)
        self.assertEqual(ctx.exception.code, 405)


if __name__ == "__main__":
    unittest.main()
