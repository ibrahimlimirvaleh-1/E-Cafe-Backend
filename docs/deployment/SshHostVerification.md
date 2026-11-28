# SSH host verification rollout

## Purpose and scope

The dev deployment requires `ECAFE_DEV_SSH_FINGERPRINT` for both SCP upload and
SSH command execution. The existing host, username, private key, source archive,
build, migration and restart commands are unchanged. This pin verifies the
server's SSH host key; it is not the deploy user's public or private login key.

The preflight runs locally on the Actions runner after checkout and before
packaging or any remote operation. It accepts exactly one canonical OpenSSH
`SHA256:` fingerprint of a 32-byte digest (43 unpadded Base64 characters).
Missing, empty, whitespace, padded, multiple or malformed values fail closed.
Values are not trimmed, logged, passed on the command line or interpolated into
shell source. The value is provided as a step-scoped environment variable.

Format validation does not authenticate the server. The two Appleboy actions
perform that check against the supplied pin during their SSH handshakes.
There is deliberately no fallback to an unverified connection.

## Required preparation, before merge or deployment

1. Open the real server through the hosting provider's trusted console or an
   independently verified administrative connection. Protect that console account.
2. Check the active SSH host key configuration. For an active Ed25519 host key,
   display its fingerprint with this read-only command on the server:

   ```sh
   ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub -E sha256
   ```

3. Use only the full `SHA256:...` field from that output, not the bit count,
   comment, key type, whole public key, or any private key. If a different host
   key is negotiated, verify and pin that actual configured key instead.
   `ssh-keyscan` by itself does not prove ownership and must not be the trust source.
4. In **both** E-Cafe-Backend and E-Cafe-Frontend, open
   Settings -> Environments -> dev -> Environment secrets -> Add secret.
   Set `ECAFE_DEV_SSH_FINGERPRINT` to the independently verified value.
   Repository secrets can be used if that is the existing policy; beware of
   an environment secret with the same name overriding the repository value.
5. Keep the existing `ECAFE_DEV_SSH_KEY` and other deploy secrets unchanged.
   The host fingerprint is public information but its stored value needs
   integrity protection. Do not replace it with an arbitrary example.
6. Only merge or deploy once both repository pins are configured. Missing pins
   intentionally stop the new workflow; this is not a business-runtime outage.

This change does not provision GitHub secrets, read production private keys,
rotate host keys, change SSH accounts or server configuration, or deploy code.

## Verification

Run the synthetic local tests without secrets or a network connection:

```sh
python3 -B -m unittest discover -s deploy/security -p 'test_*.py' -v
```

The existing backend CI discovery includes these tests. A non-empty, correctly
formatted but wrong pin passes format validation and must then fail the actual
SSH host-key check. The pin is bound to both SCP and remote-command actions.

In an isolated test server/workflow, verify:

- Correct trusted pin and correct login key: upload and a harmless command work.
- Wrong but well-formed pin: both actions refuse connection before writing files
  or running the remote command.
- Empty, unset or malformed pin: preflight fails before either action starts.
- Correct pin but unauthorized login key: authentication still fails; the pin
  does not grant login rights.
- A secret containing shell syntax is rejected as data and is never executed.

After an authorized deployment, confirm the validation step, SCP and SSH steps
succeed and the existing API health/readiness checks pass. Do not change a live
secret to a deliberately wrong value just to test failure; use an isolated job.
Do not claim a live deployment was verified based on format tests alone.

## Host-key rotation or mismatch

A mismatch is a stop signal. Verify the server identity and the reason for the
change through the trusted console before replacing the pin in both repos.
Coordinate a planned host-key rotation with deployment owners. Never work around
the error by removing `fingerprint`, passing an empty value, using
`continue-on-error`, or accepting a freshly scanned key without verification.
Reverting to a workflow without verification is not a safe rollback.

## References

- [OpenSSH ssh-keygen](https://man.openbsd.org/ssh-keygen.1)
- [GitHub Actions secrets](https://docs.github.com/en/actions/how-tos/write-workflows/choose-what-workflows-do/use-secrets)
- [SCP action inputs](https://github.com/appleboy/scp-action/blob/v0.1.7/action.yml)
- [SSH action inputs](https://github.com/appleboy/ssh-action/blob/v1.0.3/action.yml)
