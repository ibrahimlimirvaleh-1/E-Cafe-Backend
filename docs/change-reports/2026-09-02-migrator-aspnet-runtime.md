# Migrator ASP.NET Runtime Fix

## Məqsəd

Backend deploy zamanı Docker image build uğurla bitirdi, amma migrator container işə düşəndə aşağıdakı xəta ilə dayanırdı:

```text
You must install or update .NET to run this application.
Framework: 'Microsoft.AspNetCore.App', version '9.0.0' (x64)
No frameworks were found.
```

## Kök Səbəb

`Dockerfile` daxilində `migrator` final image-i `mcr.microsoft.com/dotnet/runtime:9.0` üzərində qurulmuşdu.

`ECafe.Migrator.dll` isə layihə asılılıqları vasitəsilə `Microsoft.AspNetCore.App` shared framework tələb edir. `dotnet/runtime` image-də bu framework olmur; o yalnız `dotnet/aspnet` image-də mövcuddur.

## Edilən Dəyişiklik

### `Dockerfile`

Migrator final stage dəyişdirildi:

```text
mcr.microsoft.com/dotnet/runtime:9.0
```

əvəzinə:

```text
mcr.microsoft.com/dotnet/aspnet:9.0
```

## Niyə Belə Daha Düzgündür?

- Publish olunmuş migrator hansı shared framework-ü tələb edirsə, runtime image də onu təmin etməlidir.
- API və migrator eyni .NET ASP.NET runtime ailəsi ilə işləyir.
- Deploy zamanı migrasiya container-i əlavə framework install etmədən işə düşür.
- Dockerfile sadə qalır; container daxilində manual `.NET install` kimi riskli və təkrarlanan addım yoxdur.

## Təsir Dairəsi

- API runtime davranışı dəyişmir.
- Database migration kodu dəyişmir.
- Yalnız migrator container-in base image-i dəyişir.

## Risk

`aspnet:9.0` image `runtime:9.0`-dan bir az böyükdür, amma deploy stabilliyi üçün doğru seçimdir. Alternativ olaraq migrator layihəsi ASP.NET shared framework tələb etməyəcək şəkildə ayrılardı, lakin bu daha böyük refaktor tələb edir.

