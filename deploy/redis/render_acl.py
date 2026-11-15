"""Generate a private Redis ACL bundle without printing credentials."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import secrets


USERS = {"CACHE": "ecafe_cache", "REALTIME": "ecafe_realtime",
         "VIEWER": "ecafe_viewer", "ADMIN": "ecafe_admin"}


def render(template, passwords):
    """Parollari SHA-256 ile ACL sablonuna yazir; aciq parol ACL-e daxil olmur."""
    for role, username in USERS.items():
        digest = hashlib.sha256(passwords[username].encode("utf-8")).hexdigest()
        template = template.replace("@" + role + "_SHA256@", digest)
    if "_SHA256@" in template:
        raise ValueError("Unresolved ACL password placeholder.")
    return template


def write_private(path, content):
    """Movcud fayli ezmeden yeni mexfi fayli yalniz sahibinin oxuya bildiyi rejimde yaradir."""
    descriptor = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(descriptor, "w", encoding="utf-8", newline="\n") as stream:
        stream.write(content)


def create_bundle(directory, template_path):
    """Her muhit ucun tesadufi ayri parollar ve tekrar yazilmayan ACL paketi yaradir."""
    directory.mkdir(mode=0o700, parents=False, exist_ok=False)
    passwords = {username: secrets.token_hex(32) for username in USERS.values()}
    template = template_path.read_text(encoding="utf-8")
    write_private(directory / "users.acl", render(template, passwords))
    write_private(directory / "credentials.json", json.dumps(passwords, indent=2) + "\n")


def main():
    """Parollari arqument ve ya loga cixarmadan CLI-den mexfi paket yaradilmasini basladir."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-directory", type=Path, required=True)
    args = parser.parse_args()
    create_bundle(args.output_directory, Path(__file__).with_name("users.acl.template"))
    print("Private Redis ACL bundle created. Credentials were not printed.")


if __name__ == "__main__":
    main()
