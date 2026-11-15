import hashlib
from pathlib import Path
import tempfile
import unittest

from render_acl import USERS, create_bundle, render


class RenderAclTests(unittest.TestCase):
    def test_passwords_are_hashed_and_default_is_disabled(self):
        passwords = {username: "not-a-live-password-" + username for username in USERS.values()}
        result = render(Path(__file__).with_name("users.acl.template").read_text(), passwords)
        self.assertTrue(result.startswith("user default reset off\n"))
        self.assertNotIn("_SHA256@", result)
        for password in passwords.values():
            self.assertNotIn(password, result)
            self.assertIn("#" + hashlib.sha256(password.encode()).hexdigest(), result)

    def test_existing_bundle_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as root:
            directory = Path(root) / "private"
            template = Path(__file__).with_name("users.acl.template")
            create_bundle(directory, template)
            original = (directory / "credentials.json").read_bytes()
            with self.assertRaises(FileExistsError):
                create_bundle(directory, template)
            self.assertEqual(original, (directory / "credentials.json").read_bytes())


if __name__ == "__main__":
    unittest.main()
