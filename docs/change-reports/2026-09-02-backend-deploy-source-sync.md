# Backend Dev Deploy Source Sync

## Məqsəd

`dev` branch-ə merge etdikdən sonra backend deploy Hetzner serverdə `git pull --ff-only origin dev` mərhələsində qırılırdı. Repo private olduğuna görə server GitHub username/token oxuya bilmirdi və deploy dayanırdı.

## Kök Səbəb

Deploy workflow serverə SSH ilə qoşulub `/opt/ecafe/backend` qovluğunda `git fetch`, `git checkout dev`, `git pull --ff-only origin dev` işlədirdi. Hetzner serverdə GitHub credential olmadıqda private repository üçün bu əmrlər uğursuz olur.

## Edilən Dəyişikliklər

### `.github/workflows/deploy-dev.yml`

- `actions/checkout@v4` əlavə edildi.
- Backend source GitHub Actions runner-də paketlənir.
- Paket `$RUNNER_TEMP/backend-source.tar.gz` içində yaradılır, sonra upload üçün workspace root-a kopyalanır.
- `appleboy/scp-action@v0.1.7` ilə paket serverə göndərilir.
- Serverdə `/opt/ecafe/backend` qovluğu paketdən yenilənir.
- Serverdə `git fetch`, `git checkout`, `git pull` əmrləri silindi.
- Docker build, migrator run və API restart əvvəlki ardıcıllıqla saxlanıldı.

## Paketdən Çıxarılan Hissələr

- `.git`
- `.vs`
- `artifacts`
- `bin`
- `obj`
- `.env`
- `.env.*`

## Niyə Belə Daha Düzgündür?

- Serverdə GitHub token və ya deploy key saxlamağa ehtiyac qalmır.
- Private repo credential problemi deploy-u qırmır.
- GitHub Actions hansı commit-i deploy etdiyini dəqiq idarə edir.
- Server yalnız source qəbul edir, Docker build və migrasiya işlədir.
- Backend deploy axını frontend deploy axını ilə eyni prinsipə gətirilir.

## Təsir Dairəsi

- Backend runtime koduna toxunulmadı.
- API, migration və biznes məntiq dəyişmədi.
- Yalnız GitHub Actions dev deploy mexanizmi dəyişdi.

## Yoxlama

- `git diff --check` ilə format yoxlanmalıdır.
- Merge-dən sonra `Deploy Backend Dev` workflow-u artıq serverdə GitHub credential axtarmamalıdır.

