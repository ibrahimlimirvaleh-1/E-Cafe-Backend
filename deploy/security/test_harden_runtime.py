"""Synthetic transformation tests; no Docker or live server is used."""
import copy
import hashlib
import tempfile
from unittest.mock import patch
from types import SimpleNamespace
from pathlib import Path
import unittest
import harden_runtime as h


class RuntimeHardeningTests(unittest.TestCase):
    """Canli transformasiyanin yalniz nezərdə tutulan saheleri deyisməsini yoxlayir."""

    def test_nginx_keeps_routing_and_removes_request_data_from_active_logs(self):
        original = "error_log /var/log/nginx/error.log warn;\nhttp {\n log_format main '$request $http_referer';\n access_log /var/log/nginx/access.log main;\n include /etc/nginx/conf.d/*.conf;\n}\n"
        result = h.nginx_text(original)
        self.assertIn("include /etc/nginx/conf.d/*.conf;", result)
        self.assertIn("access_log /var/log/nginx/access.log ecafe_private;", result)
        self.assertIn("error_log /dev/null emerg;", result)
        active = next(line for line in result.splitlines() if "log_format ecafe_private " in line)
        for field in ("$request ", "$http_referer", "$http_user_agent", "$args", "$remote_addr", "$uri"):
            self.assertNotIn(field, active)
        self.assertEqual(result, h.nginx_text(result))

    def test_unexpected_nginx_layout_is_rejected(self):
        with self.assertRaises(ValueError):
            h.nginx_text("http { access_log /tmp/a; access_log /tmp/b; }")

    def test_env_preserves_every_unrelated_secret_and_updates_duplicates(self):
        source = "# keep\nPassword=synthetic-secret\nASPNETCORE_ENVIRONMENT=Development\nASPNETCORE_ENVIRONMENT=Local\n"
        result = h.environment_text(source, {"ASPNETCORE_ENVIRONMENT": "Production", "Sentry__EnableLogs": "false"})
        self.assertIn("Password=synthetic-secret\n", result)
        self.assertNotIn("Development", result)
        self.assertEqual(2, result.count("ASPNETCORE_ENVIRONMENT=Production"))
        self.assertEqual(result, h.environment_text(result, {"ASPNETCORE_ENVIRONMENT": "Production", "Sentry__EnableLogs": "false"}))

    def test_compose_preserves_database_redis_secrets_and_mounts(self):
        source = {"services": {"api": {"environment": {"Jwt__Key": "synthetic-secret"}, "volumes": ["keys:/keys"]},
                               "frontend": {}, "caddy": {}, "postgres": {"volumes": ["db:/data"]}, "redis": {"command": "unchanged"}}, "volumes": {"db": {}, "keys": {}}}
        before = copy.deepcopy(source)
        result = h.compose_config(source, Path("/opt/ecafe/frontend-nginx-private.conf"), "172.18.0.7")
        self.assertEqual(before, source)
        self.assertEqual(source["services"]["postgres"], result["services"]["postgres"])
        self.assertEqual(source["services"]["redis"], result["services"]["redis"])
        self.assertEqual(source["services"]["api"]["volumes"], result["services"]["api"]["volumes"])
        self.assertEqual("synthetic-secret", result["services"]["api"]["environment"]["Jwt__Key"])
        self.assertEqual("Production", result["services"]["api"]["environment"]["ASPNETCORE_ENVIRONMENT"])
        self.assertEqual("172.18.0.7", result["services"]["api"]["environment"]["ForwardedHeaders__KnownProxies__0"])
        self.assertEqual(result, h.compose_config(result, Path("/opt/ecafe/frontend-nginx-private.conf"), "172.18.0.7"))

    def test_public_proxy_is_rejected(self):
        with self.assertRaises(ValueError):
            h.compose_config({}, Path("/opt/ecafe/nginx.conf"), "8.8.8.8")

    def test_caddy_privacy_keeps_routes_and_targets_only_ecafe_referrer(self):
        source = 'app.example.test {\n reverse_proxy api:8080\n}\nother.example.test {\n reverse_proxy portfolio:80\n}\n'
        result = h.caddy_text(source, 'app.example.test')
        self.assertIn('request delete', result)
        self.assertIn('header >Referrer-Policy "no-referrer"', result)
        self.assertIn('other.example.test {\n reverse_proxy portfolio:80\n}', result)
        self.assertEqual(result, h.caddy_text(result, 'app.example.test'))

    def test_existing_unknown_caddy_global_options_are_not_overwritten(self):
        with self.assertRaises(ValueError):
            h.caddy_text('{ debug }\napp.example.test { respond ok }', 'app.example.test')

    def test_existing_unrelated_nginx_mount_is_not_replaced(self):
        source = {"services": {"api": {}, "caddy": {}, "frontend": {"volumes": ["/other/file:/etc/nginx/nginx.conf:ro"]}}}
        with self.assertRaises(ValueError):
            h.compose_config(source, Path("/opt/ecafe/nginx.conf"), "172.18.0.7")

    def test_mobile_release_and_apk_mount_are_preserved_without_other_changes(self):
        source = {"services": {"api": {"environment": {"Jwt__Key": "safe"}, "volumes": []}}}
        effective = {"services": {"api": {"environment": {"Jwt__Key": "safe"}, "volumes": []}}}
        live = {"Config": {"Env": ["Jwt__Key=safe", "MobileApp__Release__Ready=true", "PATH=/bin"]},
                "Mounts": [{"Type": "bind", "Source": "/opt/ecafe/private-releases", "Destination": "/var/lib/ecafe/private-releases", "RW": False}]}
        result = h.preserve_live_api(source, effective, live)
        self.assertEqual("true", result["services"]["api"]["environment"]["MobileApp__Release__Ready"])
        self.assertEqual("safe", result["services"]["api"]["environment"]["Jwt__Key"])
        self.assertTrue(result["services"]["api"]["volumes"][0]["read_only"])
        self.assertEqual([], source["services"]["api"]["volumes"])

    def test_unknown_live_secret_drift_is_rejected_before_any_write(self):
        source = {"services": {"api": {"environment": {"Jwt__Key": "old"}}}}
        live = {"Config": {"Env": ["Jwt__Key=different"]}, "Mounts": []}
        with self.assertRaises(ValueError):
            h.preserve_live_api(source, source, live)

    def test_readiness_preserves_host_filtering_and_uses_trusted_host(self):
        with patch("harden_runtime.subprocess.run", return_value=SimpleNamespace(returncode=0, stdout=b'{"status":"Healthy"}')) as run:
            h.wait_ready("app.example.test")
        arguments = run.call_args.args[0]
        self.assertIn("Host: app.example.test", arguments)
        self.assertIn("X-Forwarded-Proto: https", arguments)
        self.assertIn("http://127.0.0.1:8080/health/ready", arguments)

    def test_activated_receipt_updates_old_release_without_reverting_api_or_push(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "private-releases").mkdir()
            data = b"synthetic-apk-only"
            digest = hashlib.sha256(data).hexdigest()
            name = "ECafe-0.1.0-build15-" + digest[:12] + ".apk"
            (root / "private-releases" / name).write_bytes(data)
            env = dict.fromkeys(h.MOBILE_SETTINGS, "old")
            env.update({"MobileApp__Release__Version": "0.1.0", "MobileApp__PushDeliveryReady": "true", "MobileApp__ExpoProjectId": "synthetic-project"})
            source = {"services": {"api": {"image": "current-api", "environment": {"Jwt__Key": "unchanged"}}}}
            receipt = {"status": "ACTIVATED", "versionCode": 15, "sha256": digest, "sizeBytes": len(data),
                       "apkPath": "/var/lib/ecafe/private-releases/" + name}
            result = h.preserve_published_mobile(source, {"environment": env}, receipt, root)
            current = result["services"]["api"]
            self.assertEqual("current-api", current["image"])
            self.assertEqual("unchanged", current["environment"]["Jwt__Key"])
            self.assertEqual("true", current["environment"]["MobileApp__PushDeliveryReady"])
            self.assertEqual("15", current["environment"]["MobileApp__Release__VersionCode"])
            self.assertEqual(digest, current["environment"]["MobileApp__Release__Sha256"])
            (root / "private-releases" / name).write_bytes(b"corrupted")
            with self.assertRaises(ValueError):
                h.preserve_published_mobile(source, {"environment": env}, receipt, root)


if __name__ == "__main__":
    unittest.main()
