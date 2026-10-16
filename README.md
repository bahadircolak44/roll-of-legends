# Roll of Legends

Unity ile geliştirilen 3D bir mobil zar/board oyunu.

> Şu an: oynanabilir ilk prototip — 28 tile'lık kare board, yapay zekâ rakibe karşı kartlı zar oyunu, yolda kılıç/iksir/tuzak/canavar, üs yıkınca kazanırsın. Güncel durum için [docs/DEV_LOG.md](docs/DEV_LOG.md).

## Gereksinimler

| | Sürüm |
|---|---|
| Unity Editor | **6000.3.26f1** (Unity 6.3 LTS) |
| Render pipeline | Universal Render Pipeline (URP) |
| Hedef platform | Android (iOS ileride, macOS gerektirir) |
| Unity Hub modülleri | Android Build Support, Android SDK & NDK Tools, OpenJDK |
| Git | Git LFS kurulu olmalı |

## Kurulum

```bash
git clone https://github.com/bahadircolak44/roll-of-legends.git
cd roll-of-legends
./scripts/setup-git.sh   # Git LFS + Unity Smart Merge ayarı (bir kez)
```

Ardından Unity Hub → **Add project from disk** → bu klasörü seç.

## Oynamak

Unity'de **Project** penceresinden `Assets/_Project/Scenes/Main.unity` sahnesini aç ve üstteki ▶ **Play** butonuna bas.
Testler: **Window → General → Test Runner → PlayMode → Run All**.

## Klasör yapısı

```
Assets/
  _Project/            # Oyuna ait her şey burada
    Animations/
    Art/               # Materials, Models, Textures, Sprites
    Audio/             # Music, SFX
    Prefabs/
    Scenes/
    ScriptableObjects/
    Scripts/           # Runtime (oyun kodu), Editor (editör araçları)
    Tests/             # PlayMode testleri
    UI/
  Settings/            # URP pipeline & renderer asset'leri
  ThirdParty/          # Asset Store / harici paketler
Packages/              # Unity Package Manager manifest
ProjectSettings/       # Proje ayarları (commit edilir)
```

## Geliştirme akışı

Branch stratejisi, commit kuralları ve PR süreci için bkz. [CONTRIBUTING.md](CONTRIBUTING.md).
AI kodlama ajanları için kurallar: [AGENTS.md](AGENTS.md).
