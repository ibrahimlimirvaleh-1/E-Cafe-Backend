"""Apply scoped ECafe runtime hardening; default is a read-only plan."""

import argparse
import copy
from datetime import datetime, timezone
import gzip
import hashlib
import ipaddress
import json
import os
from pathlib import Path, PurePosixPath
import re
import subprocess
import tempfile
import time
from urllib.parse import urlsplit


LOGGING = {"driver": "json-file", "options": {"max-size": "10m", "max-file": "3"}}
SERVICES = ("api", "frontend", "caddy")
CADDY_LOG_PREFIX = """# ECafe request privacy
{
    log default {
        format filter {
            wrap json
            fields {
                request delete
                uri delete
                headers delete
            }
        }
    }
}

"""
MOBILE_SETTINGS = {"MobileApp__Release__Sha256", "MobileApp__PushDeliveryReady", "MobileApp__Release__ApkPath",
                   "MobileApp__Release__SizeBytes", "MobileApp__Release__Ready", "MobileApp__ExpoProjectId",
                   "MobileApp__Release__VersionCode", "MobileApp__Release__Version"}


def command(*args, cwd=None):
    """Komanda cixisini yaddaşda saxlayir; server sirleri ve log metni terminala verilmir."""
    result = subprocess.run(args, cwd=cwd, capture_output=True, timeout=180)
    if result.returncode:
        raise RuntimeError("Runtime command failed; output withheld.")
    return result.stdout


def private_write(path, data, mode=0o600):
    """Eyni qovluqda atomik yazir; symlink ustunden basqa fayla yazmaga icaze vermir."""
    if path.is_symlink():
        raise ValueError("Symlink target is not allowed.")
    fd, name = tempfile.mkstemp(dir=path.parent, prefix=".ecafe-hardening-")
    try:
        os.fchmod(fd, mode)
        with os.fdopen(fd, "wb") as stream:
            stream.write(data)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(name, path)
    finally:
        if os.path.exists(name):
            os.unlink(name)


def environment_text(source, updates):
    """Yalniz adlari aciq verilen env setirlerini deyisir; parollar ve qalan setirler qorunur."""
    pending = dict(updates)
    lines = []
    for line in source.splitlines():
        key = line.split("=", 1)[0]
        if key in updates:
            lines.append(key + "=" + updates[key])
            pending.pop(key, None)
        else:
            lines.append(line)
    lines.extend(key + "=" + value for key, value in pending.items())
    return "\n".join(lines) + "\n"


def nginx_text(source):
    """Sorğu URL-i, referer, header ve IP olmadan status/metod/vaxt loglayir; request protokoluna toxunmur."""
    access = re.compile(r"^[ \t]*access_log\s+[^;]+;", re.M)
    errors = re.compile(r"^[ \t]*error_log\s+[^;]+;", re.M)
    private_error = "    error_log /dev/null emerg;"
    http = re.compile(r"^[ \t]*http\s*\{\s*$", re.M)
    if len(access.findall(source)) != 1 or len(http.findall(source)) != 1 \
            or len(errors.findall(source)) not in (1, 2):
        raise ValueError("Unexpected Nginx layout; manual review required.")
    if len(errors.findall(source)) == 2 and private_error not in source:
        raise ValueError("Unknown Nginx error log requires manual review.")
    declaration = "    log_format ecafe_private 'method=$request_method status=$status bytes=$body_bytes_sent elapsed=$request_time';\n"
    if "log_format ecafe_private " not in source:
        source = access.sub(lambda match: declaration + match.group(), source, count=1)
    source = access.sub("    access_log /var/log/nginx/access.log ecafe_private;", source)
    source = errors.sub("error_log stderr notice;", source, count=1)
    if private_error not in source:
        source = http.sub(lambda match: match.group() + "\n" + private_error, source, count=1)
    return source


def caddy_text(source, host):
    """Caddy xeta loglarindan request/uri/header datasini cixarir; yalniz ECafe hostuna no-referrer cavabi elave edir."""
    if not source.startswith(CADDY_LOG_PREFIX):
        if source.lstrip().startswith("{"):
            raise ValueError("An existing Caddy global block requires manual review.")
        source = CADDY_LOG_PREFIX + source
    declaration = '    header >Referrer-Policy "no-referrer" # ECafe private referrer'
    if declaration not in source:
        pattern = re.compile(r"^(?:https?://)?" + re.escape(host) + r"(?::\d+)?\s*\{", re.M)
        if len(pattern.findall(source)) != 1:
            raise ValueError("Expected one ECafe Caddy site block.")
        source = pattern.sub(lambda match: match.group() + "\n" + declaration, source, count=1)
    return source


def validate_caddy(source):
    """Caddy-nin oz parseri ile yazmadan yoxlayir; JSON konfiqurasiyasi ve xetalar cixisa verilmir."""
    result = subprocess.run(["docker", "exec", "-i", "ecafe-caddy", "caddy", "adapt", "--config", "/dev/stdin", "--adapter", "caddyfile"],
                            input=source, capture_output=True, timeout=30)
    if result.returncode:
        raise ValueError("Caddy validation failed; original file was not changed.")


def compose_config(source, nginx_path, proxy_ip):
    """Yalniz API rejimi, proxy etibari, uc servis log limiti ve Nginx bind mount-u deyisir."""
    if not ipaddress.ip_address(proxy_ip).is_private:
        raise ValueError("Only a private reverse proxy address is allowed.")
    result = copy.deepcopy(source)
    services = result["services"]
    for name in SERVICES:
        services[name]["logging"] = copy.deepcopy(LOGGING)
    env = services["api"].setdefault("environment", {})
    if not isinstance(env, dict):
        raise ValueError("Expected mapping-style API environment.")
    env.update({"ASPNETCORE_ENVIRONMENT": "Production", "Sentry__EnableLogs": "false",
                "Sentry__Debug": "false", "ForwardedHeaders__KnownProxies__0": proxy_ip})
    target = "/etc/nginx/nginx.conf"
    volumes = services["frontend"].setdefault("volumes", [])
    for volume in volumes:
        destination = volume.get("target") if isinstance(volume, dict) else volume.split(":")[1]
        if destination == target:
            if volume != {"type": "bind", "source": str(nginx_path), "target": target, "read_only": True}:
                raise ValueError("An existing Nginx mount requires manual review.")
            break
    else:
        volumes.append({"type": "bind", "source": str(nginx_path), "target": target, "read_only": True})
    return result


def container(name):
    """Yalniz bu layiheye aid aciq adi verilen konteyneri oxuyur; environment cixisa yazilmir."""
    return json.loads(command("docker", "inspect", "ecafe-" + name))[0]


def proxy_address():
    """Caddy-nin API ile ortaq Docker sebəkesindeki cari IP-sini qaytarir; internetden proxy qebul edilmir."""
    api = container("api")["NetworkSettings"]["Networks"]
    proxy = container("caddy")["NetworkSettings"]["Networks"]
    shared = set(api).intersection(proxy)
    if len(shared) != 1:
        raise ValueError("Expected exactly one shared API/proxy network.")
    return proxy[shared.pop()]["IPAddress"]


def preserve_live_api(baseline, effective, live):
    """Compose-da unudulmus mobil release/push ayarlarini ve APK bind mount-unu qoruyur; namelum secret drift-de dayanir."""
    result = copy.deepcopy(baseline)
    live_env = dict(value.split("=", 1) for value in live["Config"]["Env"] if "=" in value)
    configured_env = effective["services"]["api"].get("environment", {})
    for key, value in live_env.items():
        if "__" not in key or configured_env.get(key) == value:
            continue
        if key not in MOBILE_SETTINGS or any(character in value for character in "$\r\n"):
            raise ValueError("Live API configuration differs from Compose; manual reconciliation required.")
        result["services"]["api"].setdefault("environment", {})[key] = value
    targets = {value["target"] for value in effective["services"]["api"].get("volumes", [])}
    for mount in live.get("Mounts", []):
        if mount["Destination"] not in targets:
            if mount["Type"] != "bind":
                raise ValueError("An extra live data volume requires manual review.")
            result["services"]["api"].setdefault("volumes", []).append({"type": "bind", "source": mount["Source"],
                "target": mount["Destination"], "read_only": not mount["RW"]})
    return result


def archive_logs(directory):
    """Konteyner yeniden yaranmazdan evvel evvelki loglari yalniz sahibin oxudugu sixilmis arxivde qoruyur."""
    for service in SERVICES:
        data = subprocess.run(["docker", "logs", "ecafe-" + service], capture_output=True, timeout=60)
        if data.returncode:
            raise RuntimeError("Log archive failed; containers were not recreated.")
        private_write(directory / (service + "-logs.gz"), gzip.compress(data.stdout + data.stderr))


def preserve_published_mobile(source, settings, receipt, root):
    """Yalniz ACTIVATED receipt ve APK hash/olcu tesdiqi ile mobil ayarlari berpa edir; API image ve secretleri deyismir."""
    if receipt.get("status") != "ACTIVATED" or set(settings.get("environment", {})) != MOBILE_SETTINGS:
        raise ValueError("Only an activated, complete mobile publication can be restored.")
    env = {key: str(value).lower() if isinstance(value, bool) else str(value) for key, value in settings["environment"].items()}
    apk = (root / "private-releases" / Path(receipt["apkPath"]).name).resolve(strict=True)
    if apk.parent != (root / "private-releases").resolve(strict=True):
        raise ValueError("APK path escaped the private release directory.")
    with apk.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    if digest != receipt["sha256"] or apk.stat().st_size != receipt["sizeBytes"]:
        raise ValueError("Published APK hash or size mismatch.")
    expected_name = "ECafe-" + env["MobileApp__Release__Version"] + "-build" + str(receipt["versionCode"]) + "-" + digest[:12] + ".apk"
    if apk.name != expected_name or str(PurePosixPath(receipt["apkPath"]).parent) != "/var/lib/ecafe/private-releases":
        raise ValueError("Published APK identity does not match the mobile version.")
    env.update({"MobileApp__Release__Ready": "true", "MobileApp__Release__ApkPath": receipt["apkPath"],
                "MobileApp__Release__VersionCode": str(receipt["versionCode"]), "MobileApp__Release__Sha256": digest,
                "MobileApp__Release__SizeBytes": str(apk.stat().st_size)})
    result = copy.deepcopy(source)
    result["services"]["api"].setdefault("environment", {}).update(env)
    mounts = result["services"]["api"].setdefault("volumes", [])
    target = "/var/lib/ecafe/private-releases"
    for mount in mounts:
        destination = mount.get("target") if isinstance(mount, dict) else mount.split(":")[1]
        if destination == target:
            break
    else:
        mounts.append({"type": "bind", "source": str(apk.parent), "target": target, "read_only": True})
    return result


def wait_ready(host):
    """Tek API-ni baslatdiqdan sonra yalniz DB baglantisinin read-only readiness neticesini gozleyir."""
    for _ in range(60):
        result = subprocess.run(["docker", "exec", "ecafe-api", "curl", "-fsS", "--max-time", "3", "-H",
                                 "X-Forwarded-Proto: https", "-H", "Host: " + host,
                                 "http://127.0.0.1:8080/health/ready"], capture_output=True)
        try:
            if result.returncode == 0 and json.loads(result.stdout).get("status") == "Healthy":
                return
        except ValueError:
            pass
        time.sleep(2)
    raise RuntimeError("Readiness failed.")


def apply(root, execute, mobile_receipt=None):
    """Ehtiyat nusxe, yoxlama, mehdud restart ve ugursuzluqda konfigurasiya rollback-i ile canli qorumani tetbiq edir."""
    import yaml

    root = root.resolve(strict=True)
    compose = root / "docker-compose.yml"
    env_file = root / "api.env"
    nginx = root / "frontend-nginx-private.conf"
    caddy = root / "Caddyfile"
    if any(path.is_symlink() for path in (compose, env_file, nginx, caddy)):
        raise ValueError("Configuration symlinks require manual review.")
    originals = {path: path.read_bytes() if path.exists() else None for path in (compose, env_file, nginx, caddy)}
    baseline = yaml.safe_load(originals[compose])
    effective = json.loads(command("docker", "compose", "-f", str(compose), "config", "--format", "json", cwd=root))
    live_api = container("api")
    baseline = preserve_live_api(baseline, effective, live_api)
    rollback_compose = yaml.safe_dump(baseline, sort_keys=False).encode()
    if mobile_receipt is not None:
        mobile_receipt = mobile_receipt.resolve(strict=True)
        if not mobile_receipt.is_relative_to(root / "deploy-backups"):
            raise ValueError("Mobile receipt must be in the deployment backup directory.")
        receipt = json.loads(mobile_receipt.read_text())
        settings = yaml.safe_load((mobile_receipt.parent / "docker-compose.override.yml").read_text())["services"]["api"]
        baseline = preserve_published_mobile(baseline, settings, receipt, root)
    required = ["Jwt__Key", "Jwt__Issuer", "Jwt__Audience", "ConnectionStrings__ECafeDb", "Redis__Connection",
                "Frontend__BaseUrl", "Email__SmtpHost", "Email__SmtpPort", "Email__Username", "Email__Password",
                "MinIO__Endpoint", "MinIO__AccessKey", "MinIO__SecretKey", "DataProtection__KeyRingPath"]
    active_env = {key.lower(): value for key, value in effective["services"]["api"].get("environment", {}).items()}
    if any(not active_env.get(key.lower()) for key in required):
        raise ValueError("Production settings incomplete; nothing changed.")
    frontend_host = urlsplit(active_env["frontend__baseurl"]).hostname
    host = active_env.get("healthchecks__host") or urlsplit(active_env.get("filestorage__publicbaseurl") or active_env["frontend__baseurl"]).hostname
    if any(not value or not re.fullmatch(r"[a-zA-Z0-9.-]+", value) for value in (host, frontend_host)):
        raise ValueError("A valid trusted frontend host is required for readiness.")
    source_nginx = command("docker", "exec", "ecafe-frontend", "cat", "/etc/nginx/nginx.conf").decode()
    full_nginx = command("docker", "exec", "ecafe-frontend", "nginx", "-T").decode()
    if len(re.findall(r"^[ \t]*access_log\s+", full_nginx, re.M)) != 1 \
            or len(re.findall(r"^[ \t]*error_log\s+", full_nginx, re.M)) != len(re.findall(r"^[ \t]*error_log\s+", source_nginx, re.M)):
        raise ValueError("A child Nginx log requires manual review.")
    hardened_nginx = nginx_text(source_nginx).encode()
    hardened_caddy = caddy_text(originals[caddy].decode(), frontend_host).encode()
    validate_caddy(hardened_caddy)
    print("Plan: private request logging; Production API; bounded api/frontend/caddy logs; preserve DB/Redis/volumes.")
    if not execute:
        print("Read-only plan. Use --apply after reviewing the deployment.")
        return
    directory = root / ("security-backup-" + datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ"))
    directory.mkdir(mode=0o700)
    for path, data in originals.items():
        if data is not None:
            private_write(directory / path.name, data)
    private_write(directory / "api-runtime.json", json.dumps(live_api).encode())
    private_write(directory / "nginx-original.conf", source_nginx.encode())
    archive_logs(directory)
    try:
        private_write(nginx, hardened_nginx, 0o644)
        private_write(caddy, hardened_caddy, 0o644)
        private_write(compose, yaml.safe_dump(compose_config(baseline, nginx, proxy_address()), sort_keys=False).encode())
        command("docker", "compose", "-f", str(compose), "config", "--quiet", cwd=root)
        force_caddy = ["--force-recreate"] if originals[caddy] != hardened_caddy else []
        command("docker", "compose", "-f", str(compose), "up", "-d", "--no-deps", "--no-build", *force_caddy, "caddy", cwd=root)
        updates = {"ASPNETCORE_ENVIRONMENT": "Production", "Sentry__EnableLogs": "false", "Sentry__Debug": "false",
                   "ForwardedHeaders__KnownProxies__0": proxy_address()}
        private_write(env_file, environment_text(originals[env_file].decode(), updates).encode())
        private_write(compose, yaml.safe_dump(compose_config(baseline, nginx, updates["ForwardedHeaders__KnownProxies__0"]), sort_keys=False).encode())
        command("docker", "compose", "-f", str(compose), "config", "--quiet", cwd=root)
        force = ["--force-recreate"] if originals[nginx] != hardened_nginx else []
        command("docker", "compose", "-f", str(compose), "up", "-d", "--no-deps", "--no-build", *force, "frontend", cwd=root)
        command("docker", "exec", "ecafe-frontend", "nginx", "-t")
        command("docker", "compose", "-f", str(compose), "up", "-d", "--no-deps", "--no-build", "api", cwd=root)
        wait_ready(host)
        print("Runtime hardening applied; readiness healthy. Private backup: " + str(directory))
    except BaseException:
        for path, data in originals.items():
            if data is None:
                if path.exists():
                    path.unlink()
            else:
                private_write(path, rollback_compose if path == compose else data, 0o644 if path in (nginx, caddy) else 0o600)
        command("docker", "compose", "-f", str(compose), "up", "-d", "--no-deps", "--no-build", *SERVICES, cwd=root)
        wait_ready(host)
        raise RuntimeError("Hardening rolled back; previous configuration restored.") from None


def main():
    """Acıq root ve --apply olmadan canli deyisiklik etmir; xeta cixisinda secret/metn yoxdur."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--mobile-receipt", type=Path)
    args = parser.parse_args()
    try:
        apply(args.root, args.apply, args.mobile_receipt)
    except Exception as error:
        print("Runtime hardening stopped (" + type(error).__name__ + "). No secret output.")
        if type(error) in (ValueError, RuntimeError):
            print(str(error))
        raise SystemExit(1) from None


if __name__ == "__main__":
    main()
