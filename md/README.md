# Cine Media Player — Documentation

## Core Docs

| Doc | Description |
|-----|-------------|
| [`PROJECT_MASTER_GUIDE.md`](./PROJECT_MASTER_GUIDE.md) | Architecture, build status, feature dashboard, PIP debug guide, shortcuts |
| [`MAIN_UI_GOLD_STANDARD.md`](./MAIN_UI_GOLD_STANDARD.md) | Visual reference — Python GTK4 design ported to Avalonia |

---

## Project Layout

```
src/
├── App/         Avalonia UI (entry point)
├── Media/       mpv + Media Foundation backends
└── Core/        Domain abstractions
```

**Build:** `dotnet build src/App/` — ✅ 0 errors
