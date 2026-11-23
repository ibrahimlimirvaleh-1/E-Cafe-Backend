# Sessiya və icazə yoxlaması: deploy və test siyahısı

Tarix: 20.11.2026.

Bu sənəd sessiya və icazə keşinin köhnəlməsi düzəlişindən sonra nə edilməli
olduğunu izah edir. Kodun texniki izahı və avtomatik test nəticələri
[dəyişiklik hesabatındadır](../change-reports/2026-11-20-authorization-cache-consistency.md).

## Nə dəyişib? Sadə nümunə

Sahibkar ofisiantın girişini bağlayır. Əvvəl Redis-də köhnə “girişə icazə var”
cavabı qalsaydı, ofisiant bir müddət işləməyə davam edə bilərdi. İndi növbəti
təhlükəsizlik yoxlaması cari bazaya baxır. Redis-dəki köhnə cavab giriş vermir.

Bu, artıq icazə alıb icraya başlamış hər əməliyyatı geri qaytarmır. Açıq qalmış
ekran və ya WebSocket də avtomatik olaraq dərhal bağlanmaya bilər. Yoxlamanın
əsası yeni qorunan sorğunun rədd edilməsidir, təkcə ekrandakı düymənin itməməsi deyil.

## Hazır olan və gözləyən işlər

- Düzəliş commiti: `690651b` (`fix/authorization-cache-consistency` branch-i).
- Avtomatik Release build: 0 xəbərdarlıq, 0 xəta.
- Avtomatik tam test paketi: 1702 keçdi, 0 xəta, 0 buraxılmış test.
- Son sərtləşdirilmiş yoxlamadan sonra 50 fokus testi də keçdi.
- Bu nəticələr canlı serverdə deploy və əl testlərinin keçməsi demək deyil.
- Aşağıdakı əl testləri bu sənəd hazırlanarkən **gözləyir**; nəticələri test edən şəxs qeyd etməlidir.
- Öz e-poçt ünvanını dəyişərkən təkrar parol və yeni ünvanın təsdiqi ayrıca **gözləyən təhlükəsizlik işidir**.

## 1. Deploy ardıcıllığı

1. Düzəlişin PR-ını yoxla. Artıq `dev`-ə birləşdirilibsə, təkrar PR açma.
2. Son commit üçün CI-də restore, audit, build və test nəticələrinin uğurlu olduğunu yoxla.
3. PR açıqdırsa, tələb olunan yoxlamalar keçdikdən sonra `dev`-ə birləşdir.
4. Deployun məhz düzəlişi daxil edən commit/image versiyasından hazırlandığını yoxla.
5. Bütün API instansiyalarını həmin versiyaya keçir. Bir instansiya köhnə qalsa,
   onun köhnə keş davranışını riskin bağlanması kimi qəbul etmək olmaz.
6. `/health/live` və `/health/ready` cavablarını, tətbiqin açılmasını və xəta loglarını yoxla.
   Təkcə health cavabı icazə testlərinin əvəzi deyil.
7. Aşağıdakı sınaqları test hesabları ilə yerinə yetir və nəticələri qeyd et.

Bu düzəliş ayrıca DB miqrasiyası və ya Redis açarlarını kütləvi silmək tələb etmir.
Mövcud Redis ACL-ləri, məxfi ayarlar, JWT açarı və Data Protection saxlanılmalıdır.
İcazə yoxlamaları yazıların getdiyi aktual primary DB-dən oxumalıdır;
gecikən read-replica bu zəmanəti vermir.

## 2. Testə hazırlıq

1. Real əməkdaşı yox, ayrıca test ofisiantı/meneceri istifadə et.
2. Chrome və Edge kimi iki ayrı brauzerdə həmin test hesabına daxil ol.
   Eyni brauzerin iki adi tabı eyni giriş məlumatını paylaşa bilər və iki cihaz testi sayılmır.
3. Sahibkar/admin idarəetməsini ayrıca profil və ya üçüncü brauzerdə aç.
4. Hər iki test brauzerində hesabın icazəsi olan bir qorunan səhifəni aç.
5. Restoran sərhədi testi üçün iki test restoranına aidiyyət lazımdır. Belə imkan
   yoxdursa, həmin sınağı “keçdi” yox, “yoxlanmadı” kimi qeyd et.
6. Hər sınaqdan sonra test hesabının əvvəlki vəziyyətini bərpa et və lazım olsa yenidən daxil ol.

## 3. Əl ilə yoxlama: addım-addım

### T1. Adi giriş və sessiyanın davam etməsi

1. Hər iki brauzerdə daxil ol və icazəli səhifəni aç.
2. Səhifəni yenilə, sonra icazəli başqa səhifəyə keç.
3. Refresh token yenilənməsini ayrıca test mühitində mövcud auth yoxlaması ilə yoxla;
   production token müddətini sınaq üçün dəyişmə.

**Gözlənilən:** qanuni istifadəçi əsassız logout olmur, normal sorğular işləyir.

### T2. İstifadəçi deaktiv edilir

1. Sahibkar/admin test əməkdaşını deaktiv etsin və uğurlu cavabı gözləsin.
2. Bundan sonra hər iki test brauzerindən yeni qorunan sorğu göndər.
3. Test hesabını yenidən aktiv et və lazım olsa yeni giriş et.

**Gözlənilən:** deaktivlikdən sonra başlayan yoxlamada giriş rədd edilir;
Redis TTL-inin bitməsini gözləmək lazım deyil. Köhnə ekranın görünməsi girişin işləməsi demək deyil.

### T3. Yalnız bir restoranın aidiyyəti alınır

1. Test hesabının A və B restoranında işlədiyini əvvəlcədən yoxla.
2. Yalnız A restoranındakı aidiyyəti ləğv et və uğurlu cavabı gözlə.
3. Həmin istifadəçi ilə A və B üçün ayrı-ayrı yeni qorunan sorğu et.

**Gözlənilən:** A üçün giriş yoxdur, B üçün mövcud qanuni giriş qalır.

### T4. Rol və ya konkret icazə dəyişir

1. Test hesabına aid rolu daha məhdud rolla əvəz et və ya test rolundan bir icazəni al.
   Canlı əməkdaşların paylaşdığı rolu sınaq məqsədilə dəyişmə.
2. Dəyişiklik tamamlandıqdan sonra əvvəl icazəli olan əməliyyata yeni sorğu göndər.
3. Qalan icazələrdən birini də yoxla.

**Gözlənilən:** alınmış icazə işləmir, qalan qanuni icazələr işləyir.
Köhnə düymə ekranda qalsa belə backend əməliyyatı qəbul etməməlidir.
Production-da dağıdıcı əməliyyatı icra etmə; belə sınağı ayrıca test mühitində apar.

### T5. Yalnız bir cihazdan çıxış

1. Hər iki test brauzerində girişin işlədiyini yoxla.
2. Birinci brauzerdə yalnız həmin cihazdan çıxış et.
3. İkinci brauzerdə icazəli sorğu göndər.
4. Ayrı test mühitində ləğv edilmiş birinci sessiyanın köhnə məlumatı ilə qorunan
   sorğunun da rədd edildiyini yoxla. Tokeni sənədə, ekrana və ya loga yazma.

**Gözlənilən:** birinci sessiya qəbul edilmir, ikinci cihaz isə işləməyə davam edir.
Təkcə logoutdan sonra login ekranının açılması serverdə ləğvi tam sübut etmir.

### T6. Bütün cihazlardan çıxış və parol bərpası

1. İki test brauzerində yeni giriş et.
2. Mövcud bütün cihazlardan çıxış axınını icra et.
3. Hər iki köhnə sessiya ilə yeni qorunan sorğu et; sonra yeni login yoxla.
4. Parol bərpasını ayrıca sınaq kimi təkrarla: bərpanı tamamla, köhnə sessiyaları
   və yeni parolla girişi yoxla.

**Gözlənilən:** hər iki köhnə sessiya rədd edilir, yeni qanuni giriş işləyir.

### T7. Biznes və bildiriş axını

1. Test rezervasiyası yarat, müştərinin gəlişini və əyləşməsini qeyd et.
2. Müştəri sifarişi yarat, ofisiant təsdiqləsin və mətbəxə göndərsin.
3. Mətbəx statuslarını dəyiş, ofisiant və müştəri bildirişlərini yoxla.
4. Stok sərfiyyatını, hesabı, test ödənişini və sessiyanın bağlanmasını yoxla.
5. Ödənilməmiş qalıqla bağlama və başqa restoran məlumatına giriş qorumalarını da yoxla.

**Gözlənilən:** mövcud biznes qaydaları qalır; icazə düzəlişi sifariş, stok,
ödəniş və real vaxt bildirişlərini pozmur. Real pul tutmayan test mühitindən istifadə et.

## 4. Redis dayanma/bərpa sınağı: yalnız ayrıca test mühitində

**Canlı serverdə Redis-i dayandırma, `FLUSHALL`/`FLUSHDB` işlətmə və ACL-i zəiflətmə.**

1. Ayrıca test DB və Redis ilə normal girişi yoxla.
2. Yalnız test Redis-ini dayandır; DB işlək qalsın.
3. Test hesabının bir icazəsini və ya aidiyyətini bazada tətbiqin idarəetmə axını ilə al.
4. Yeni qorunan sorğu et: köhnə icazə qəbul edilməməlidir.
5. İcazəsi qalan test istifadəçisinin auth yoxlamasının işlədiyini də yoxla.
6. Redis-i bərpa et və eyni yoxlamaları təkrarla: alınmış icazə geri qayıtmamalıdır.
7. Real vaxt bağlantısının bərpasını və ekrandakı məlumatın yenilənməsini yoxla.
   Redis dayanarkən bildirişlərin problemsiz çatmasına bu auth düzəlişi zəmanət vermir.
8. DB nasazlığını yalnız ayrıca test mühitində yoxla: DB təsdiqi alınmırsa,
   köhnə Redis cavabı qorunan sorğunu uğurlu etməməlidir. Nasazlıq cavabı mütləq `403` olmaya bilər.

Köhnə keş yazısı və paralel sorğu ssenariləri avtomatik testlərdə də yoxlanıb.
Canlı Redis-də köhnə icazələri qəsdən yazmağa ehtiyac yoxdur.

## 5. Nəticəni necə qeyd etməli?

| Yoxlama | İlkin vəziyyət | Faktiki nəticə / qeyd |
| --- | --- | --- |
| PR/CI, deploy commit-i, bütün instansiyaların versiyası | Gözləyir | |
| T1: giriş və sessiya davamlılığı | Gözləyir | |
| T2: deaktiv istifadəçi | Gözləyir | |
| T3: restoran sərhədi | Gözləyir | |
| T4: rol/icazə dəyişikliyi | Gözləyir | |
| T5: tək cihazdan çıxış | Gözləyir | |
| T6: bütün cihazlar və parol bərpası | Gözləyir | |
| T7: biznes və bildiriş axını | Gözləyir | |
| Ayrı test mühitində Redis dayanma/bərpa və DB nasazlığı | Gözləyir | |

Qeydə deploy commit/image versiyasını, tarix-saatı, mühiti, test edən şəxsi və
müşahidəni əlavə et. Parol, token, cookie və connection string əlavə etmə.
Problem çıxarsa gözlənilən və faktiki nəticəni, məxfi olmayan trace ID-ni qeyd et.

Deploydan sonra DB bağlantıları, sorğu gecikməsi və auth/server xətaları əvvəlki
normal səviyyə ilə müqayisə edilməlidir. Təhlükəsizlik oxunuşları artıq DB-dən
gəldiyi üçün DB yükünə nəzarət vacibdir. Köhnə versiyaya rollback bu riski yenidən
aça bilər; rollback təhlükəsizlik məsulunun qiymətləndirməsi ilə aparılmalıdır.

## 6. Növbəti təhlükəsizlik işi: e-poçt dəyişikliyi

**Status: gözləyir. Bu sənədləşdirmə həmin funksiyanı tətbiq etmir.**

Məqsəd: açıq qalmış hesabı əldə edən şəxs hesabın e-poçtunu sadəcə başqa ünvana
dəyişib hesabı özünə keçirməsin. Təklif olunan qəbul meyarları:

1. İstifadəçinin öz e-poçtunu dəyişməsi üçün təkrar parol və ya hesaba uyğun güclü yenidən təsdiq tələb edilsin.
2. Yeni ünvana vaxtı məhdud, birdəfəlik təsdiq göndərilsin.
3. Təsdiq tamamlanana qədər əvvəlki e-poçt qüvvədə qalsın.
4. Sorğu yalnız hesab sahibinə aid olsun; başqa hesabın e-poçtu dəyişdirilə bilməsin.
5. Tokenin köhnəlməsi, təkrar/paralel istifadəsi, yeni sorğunun köhnəsini əvəz etməsi
   və ünvanın başqa hesabda olması təhlükəsiz idarə edilsin.
6. Sorğu limiti, audit və əvvəlki ünvana təhlükəsizlik xəbərdarlığı nəzərə alınsın;
   cavablar başqa hesabların mövcudluğunu açıqlamasın.
7. Sessiyaların ləğvi siyasəti ayrıca müəyyənləşdirilsin; mövcud login, bərpa,
   sessiya və restoran icazə qorumaları saxlanılsın.

İcradan əvvəl mövcud kod yenidən yoxlanmalıdır. Bu siyahı yeni auditin və ya
bütün təhlükəsizlik risklərinin bitdiyi barədə təsdiqin əvəzi deyil.
