# UI Scene Structure

Phan tich cau truc giao dien cho 1 scene. Cac node duoc ghi theo dang cay, co danh dau loai thanh phan.

**Legend:**
- `STATIC_TEXT`: Text co dinh
- `DYNAMIC_TEXT`: Text thay doi theo du lieu
- `STATIC_IMAGE`: Anh co dinh
- `INTERACTIVE`: Thanh phan tuong tac
- `PREFAB_CANDIDATE`: Nen tach thanh prefab

```
Screen
├── HEADER
│   ├── DateText (DYNAMIC_TEXT)
│   ├── CoinPanel
│   │   ├── PanelBackground (STATIC_IMAGE)
│   │   ├── CoinIcon (STATIC_IMAGE)
│   │   └── CoinValue (DYNAMIC_TEXT)
│   └── StreakPanel
│       ├── PanelBackground (STATIC_IMAGE)
│       ├── FireIcon (STATIC_IMAGE)
│       └── StreakValue (DYNAMIC_TEXT)
├── BODY
│   ├── AppTitleLabel (STATIC_TEXT)
│   ├── ProgressCard (PREFAB_CANDIDATE)
│   │   ├── CardBackground (STATIC_IMAGE)
│   │   ├── LevelLabel (STATIC_TEXT)
│   │   ├── LevelValue (DYNAMIC_TEXT)
│   │   ├── StageProgressImage (STATIC_IMAGE)     <-- Da gop thanh 1 hinh tinh
│   │   └── StageProgressLabel (STATIC_TEXT)
│   └── PlayButton (INTERACTIVE)
│       ├── ButtonBackground (STATIC_IMAGE)
│       └── PlayText (STATIC_TEXT)
└── NAVIGATION
    ├── NavBackground (STATIC_IMAGE)
    ├── ActiveTabIndicator (STATIC_IMAGE)
    └── TabListContainer
        ├── HomeTab_ItemTemplate (INTERACTIVE) (PREFAB_CANDIDATE)
        │   ├── TabIcon (STATIC_IMAGE)
        │   └── TabLabel (STATIC_TEXT)
        ├── ChallengeTab_Item (INTERACTIVE)
        │   ├── TabIcon (STATIC_IMAGE)
        │   └── TabLabel (STATIC_TEXT)
        ├── RankingTab_Item (INTERACTIVE)
        │   ├── TabIcon (STATIC_IMAGE)
        │   └── TabLabel (STATIC_TEXT)
        └── SettingTab_Item (INTERACTIVE)
            ├── TabIcon (STATIC_IMAGE)
            └── TabLabel (STATIC_TEXT)
```
