"""Real isolated Docker/Caddy502 privacy probe; uses no production endpoints."""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid
from harden_runtime import caddy_text


def main():
    """Yalniz ayrica loopback konteynerinde sintetik secret ve xeta loglarini yoxlayib oz konteynerini baglayir."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--docker', default='docker')
    args = parser.parse_args()
    name = 'ecafe-log-probe-' + uuid.uuid4().hex
    with tempfile.TemporaryDirectory() as directory:
        path = Path(directory) / 'Caddyfile'
        path.write_text(caddy_text('http://app.example.test:8080 {\n @health path /health\n respond @health "ok"\n reverse_proxy 127.0.0.1:1\n}\n', 'app.example.test'), encoding='utf-8')
        try:
            subprocess.run([args.docker, 'run', '--detach', '--name', name, '--publish', '127.0.0.1::8080',
                '--mount', 'type=bind,source='+str(path)+',target=/etc/caddy/Caddyfile,readonly', 'caddy:2.8-alpine'], check=True, capture_output=True)
            address = subprocess.check_output([args.docker, 'port', name, '8080'], text=True).strip()
            marker = 'ECAFE_SYNTHETIC_PRIVATE_' + uuid.uuid4().hex
            for _ in range(30):
                try:
                    request = urllib.request.Request('http://'+address+'/hubs/user-events?access_token='+marker,
                        headers={'Host':'app.example.test:8080', 'Authorization':'Bearer '+marker,
                                 'Cookie':'session='+marker, 'Referer':'https://app.example.test/?token='+marker, 'User-Agent':marker})
                    urllib.request.urlopen(request, timeout=2)
                except urllib.error.HTTPError as error:
                    assert error.code == 502
                    break
                except OSError:
                    time.sleep(.2)
            else:
                raise RuntimeError('Isolated Caddy did not become ready.')
            health=urllib.request.Request('http://'+address+'/health',headers={'Host':'app.example.test:8080'})
            with urllib.request.urlopen(health,timeout=2) as response:
                assert response.code==200 and response.headers['Referrer-Policy']=='no-referrer'
            result = subprocess.run([args.docker,'logs',name], capture_output=True,text=True,check=True)
            logs=result.stdout+result.stderr
            assert marker not in logs
            events=[]
            for line in logs.splitlines():
                try: events.append(json.loads(line))
                except ValueError: pass
            errors=[event for event in events if event.get('logger')=='http.log.error']
            assert errors and all('request' not in event for event in errors)
            print('PASS: real Caddy502 retained safe error diagnostics, discarded request/query/auth/cookie/referer/user-agent, and set no-referrer.')
        finally:
            subprocess.run([args.docker,'rm','--force','--volumes',name],capture_output=True)


if __name__ == '__main__':
    main()
