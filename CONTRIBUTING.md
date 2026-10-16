# Geliştirme Akışı

Bu repo **GitHub Flow** ile ilerler: `main` her zaman çalışır durumdadır, tüm değişiklikler kısa ömürlü branch'lerde yapılıp Pull Request ile `main`'e girer.

## Branch'ler

| Branch | Amaç | Örnek |
|---|---|---|
| `main` | Her zaman açılabilir ve oynanabilir. Doğrudan push **yapılmaz** (korumalı). | — |
| `feature/<kısa-ad>` | Yeni özellik / sistem | `feature/player-movement` |
| `fix/<kısa-ad>` | Hata düzeltme | `fix/camera-jitter` |
| `art/<kısa-ad>` | Sadece asset / sahne / görsel işi | `art/level-01-lighting` |
| `chore/<kısa-ad>` | Ayar, paket güncellemesi, refactor | `chore/upgrade-urp` |

## Adım adım

```bash
git switch main && git pull
git switch -c feature/player-movement
# ... geliştir, küçük ve anlamlı commit'ler at ...
git push -u origin feature/player-movement
gh pr create --fill
```

PR'lar **squash merge** ile birleştirilir, merge sonrası branch otomatik silinir.

## Commit mesajları

[Conventional Commits](https://www.conventionalcommits.org/) formatı:

```
feat: add player jump
fix: prevent double input on touch release
chore: bump URP to 17.3.x
art: add level 01 skybox
docs: update setup steps
test: cover score calculation
```

## Unity kuralları

- **Her asset'in `.meta` dosyası asset ile birlikte commit edilir.** Meta dosyası olmayan asset referanslarını kaybeder.
- Dosya taşıma/yeniden adlandırma **Unity Editor içinden** yapılır (meta GUID'leri korunur).
- Aynı sahne (`.unity`) üzerinde iki branch'te aynı anda çalışmaktan kaçının; mümkünse işi prefab'lara bölün.
- Kendi kodumuz ve asset'lerimiz `Assets/_Project/` altında; üçüncü parti paketler `Assets/ThirdParty/` altında.
- Büyük binary dosyalar (görsel, ses, model, font) `.gitattributes` sayesinde otomatik olarak Git LFS'e gider.

## Sürümler

Oynanabilir bir noktaya gelindiğinde `main` üzerinde tag atılır: `v0.1.0`, `v0.2.0`, ...
