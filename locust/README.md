# GameGaraj Logical Load Runner

Bu klasor, canli GameGaraj ortamini dusuk overhead ile test etmek icin logical user runner icerir.

Runner 10k kullaniciyi Python task/greenlet olarak dogurmaz. Kullanicilari bellekte state olarak tutar ve hedeflenen sabit RPS kadar gercekci aksiyon uretir.

## Calistirma

```powershell
./run-logical.ps1 -LogicalUsers 10000 -Rps 125 -Seconds 60 -MaxConnections 200
```

Temkinli baslangic:

```powershell
./run-logical.ps1 -LogicalUsers 1000 -Rps 50 -Seconds 60 -MaxConnections 100
```

Bagimliliklari daha once kurduysan:

```powershell
./run-logical.ps1 -LogicalUsers 10000 -Rps 125 -Seconds 60 -MaxConnections 200 -SkipInstall
```

Terminal ekrani varsayilan olarak saniyede 1 kez ayni yerde guncellenir. Gecmis ozetler yine `summary.log` ve `stats.csv` dosyalarina yazilir.

Satir satir eski tarz cikti istersen:

```powershell
./run-logical.ps1 -LogicalUsers 10000 -Rps 125 -Seconds 60 -NoLiveConsole
```

## Kullanici Akisi

Her logical user su state'i tasir:

1. Arama yapar.
2. Urun detayina gider.
3. Farkli 3 urunu sepete ekler.
4. Sepeti goruntuler.
5. Sepette 2 urunun adedini artirir.
6. Sepeti tekrar goruntuler.
7. 1 urunu siler.
8. Sepeti tekrar kontrol eder.
9. Ek bir urun gezintisi yapar.

## Loglar

Her kosu icin yeni bir klasor olusur:

```text
locust/results/yyyyMMdd-HHmmss/
```

Uretilen dosyalar:

```text
summary.log       Konsolda gorunen ozetlerin aynisi
stats.csv         Endpoint bazli periyodik metrikler
final_stats.json  Final snapshot
```

`locust/results/` git tarafindan ignore edilir.
