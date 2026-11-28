"""Synthetic SSH deploy preflight tests; no real keys, network or server is used."""

import base64
import os
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch

import validate_ssh_fingerprint as validator


VALID = "SHA256:" + base64.b64encode(bytes(range(32))).decode("ascii").rstrip("=")


class SshFingerprintTests(unittest.TestCase):
    """Deploy oncesi fail-closed yoxlamani yalniz sintetik izlerle sinayir."""

    def test_accepts_canonical_sha256_fingerprints(self):
        for digest in (bytes(range(32)), bytes(32), bytes([255]) * 32):
            with self.subTest(digest=digest):
                value = "SHA256:" + base64.b64encode(digest).decode("ascii").rstrip("=")
                self.assertTrue(validator.is_valid_fingerprint(value))

    def test_rejects_missing_empty_and_whitespace_values(self):
        for value in (None, "", " ", "\t", "\n", "\r\n"):
            with self.subTest(value=value):
                self.assertFalse(validator.is_valid_fingerprint(value))

    def test_rejects_modified_or_untrusted_pin_formats(self):
        for value in (
            VALID + "=", VALID + "\n", VALID + "\r\n", " " + VALID, VALID + " ",
            VALID + "," + VALID, VALID + "\n" + VALID, VALID.lower(),
            VALID.replace("SHA256:", "MD5:"), "SHA256:" + "A" * 42,
            "SHA256:" + "A" * 44, "SHA256:" + "_" * 43,
            "ssh-ed25519 AAAA example", "256 " + VALID + " example (ED25519)",
            "$(touch /tmp/not-a-pin)", "${{ secrets.ANOTHER_SECRET }}", "SHA256:\x00",
            "SHA256:" + "A" * 42 + "B",
        ):
            with self.subTest(value=value):
                self.assertFalse(validator.is_valid_fingerprint(value))

    def test_reads_only_the_expected_environment_variable(self):
        with patch.dict(os.environ, {validator.FINGERPRINT_ENV: VALID}, clear=True):
            with patch("builtins.print") as output:
                self.assertEqual(0, validator.main())
                self.assertNotIn(VALID, str(output.call_args_list))

        with patch.dict(os.environ, {"INPUT_FINGERPRINT": VALID}, clear=True):
            with patch("builtins.print"):
                self.assertEqual(1, validator.main())

    def test_real_entrypoint_fails_without_echoing_invalid_secret(self):
        for value in (None, "", " \n", VALID + "\n", "synthetic-invalid-secret-value"):
            with self.subTest(value=value):
                result = self.run_preflight(value)
                self.assertEqual(1, result.returncode)
                self.assertIn("::error::", result.stderr)
                self.assertNotIn("Traceback", result.stderr)
                if value and value.strip():
                    self.assertNotIn(value, result.stdout + result.stderr)

    def test_real_entrypoint_accepts_pin_without_echoing_it(self):
        result = self.run_preflight(VALID)
        self.assertEqual(0, result.returncode)
        self.assertEqual("", result.stderr)
        self.assertNotIn(VALID, result.stdout)

    def run_preflight(self, value):
        """Workflow-un isletdiyi fayli ayri prosesde, real SSH olmadan cagirir."""
        env = os.environ.copy()
        env.pop(validator.FINGERPRINT_ENV, None)
        if value is not None:
            env[validator.FINGERPRINT_ENV] = value
        return subprocess.run(
            [sys.executable, "-B", str(Path(validator.__file__).resolve())],
            env=env, capture_output=True, text=True, timeout=10, check=False,
        )


if __name__ == "__main__":
    unittest.main()
