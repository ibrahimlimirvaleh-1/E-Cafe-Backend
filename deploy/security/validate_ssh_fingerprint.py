"""Validate the configured SSH host pin locally, without connecting to a server."""

import base64
import binascii
import os
import re
import sys


FINGERPRINT_ENV = "ECAFE_DEV_SSH_FINGERPRINT"


def is_valid_fingerprint(value: str | None) -> bool:
    """Tek, kanonik SHA256 izini yoxlayir; bos ve deyisdirilmis girisi qebul etmir."""
    if value is None or re.fullmatch(r"SHA256:[A-Za-z0-9+/]{43}", value) is None:
        return False

    encoded = value.removeprefix("SHA256:")
    try:
        digest = base64.b64decode(encoded + "=", validate=True)
    except (ValueError, binascii.Error):
        return False

    return len(digest) == 32 and base64.b64encode(digest).decode("ascii").rstrip("=") == encoded


def main() -> int:
    """Secret-i yalniz muhitden oxuyur; qiymeti loglamadan deploy-u dayandirir."""
    if not is_valid_fingerprint(os.environ.get(FINGERPRINT_ENV)):
        print(
            "::error::ECAFE_DEV_SSH_FINGERPRINT must contain one canonical OpenSSH "
            "SHA256 host fingerprint. Verify it through a trusted server console "
            "and configure the dev environment secret before deploying.",
            file=sys.stderr,
        )
        return 1

    print("SSH host fingerprint format validated; SCP and SSH will verify the host key.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
