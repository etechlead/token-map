# TokenMap - Status

## Текущее состояние
- Статус проекта: **MVP завершён**
- Основная платформа MVP: **Windows**
- Вторичная платформа MVP: **macOS smoke/publish baseline**

## Что уже есть в MVP
- Выбор папки, повторный анализ, отмена, progress/state messages.
- Scanner структуры проекта с `.gitignore`, `.ignore`, default excludes и user excludes.
- Локальный подсчёт токенов через `Microsoft.ML.Tokenizers` с профилями `o200k_base`, `cl100k_base`, `p50k_base`.
- Интеграция `tokei` с merge language/code/comments/blanks и fallback на частичные LOC-метрики.
- In-memory cache, batched progress reporting и устойчивость к частичным FS/tool errors.
- Tree view, summary strip, details panel и treemap в одном окне.
- Hover tooltip, persistent selection highlight и sync `Tree ↔ Treemap ↔ Details`.
- Автоматическое раскрытие пути в дереве при выборе узла из treemap.
- Проверенный `win-x64` publish и настроенный secondary target `osx-arm64`.

## Зафиксированные решения
- Стек MVP: `.NET 10 + Avalonia stable + CommunityToolkit.Mvvm`.
- `Clever.TokenMap.Core` не зависит от Avalonia.
- `Clever.TokenMap.App` работает с файловой системой только через сервисы/контракты.
- Treemap реализован одним custom control без visual/control на каждый прямоугольник.
- `tokei` используется как local sidecar и источник truth для language / code / comments / blanks, если статистика доступна.
- Основной publish-target: `win-x64`.
- `single-file`, `Native AOT`, installer/signing и Linux polish вынесены в post-MVP.

## Документация
- Документация синхронизирована с кодом по состоянию на 2026-03-22.
- Политика документации зафиксирована в [AGENTS.md](/Z:/Projects/My/tokenmap/src/AGENTS.md): документы отражают только текущее состояние или запланированную работу; историчность остаётся в git.

## Последняя проверка
- Дата: 2026-03-22
- Что проверено:
  - `dotnet restore`
  - `dotnet build Clever.TokenMap.sln`
  - `dotnet test Clever.TokenMap.sln --no-build`

## Как запустить
- Dev run:
  - `dotnet run --project src/Clever.TokenMap.App/Clever.TokenMap.App.csproj`
- Windows publish:
  - `dotnet publish src/Clever.TokenMap.App/Clever.TokenMap.App.csproj -c Release -r win-x64 --self-contained false -o artifacts/publish/win-x64`
  - запускать `artifacts/publish/win-x64/Clever.TokenMap.App.exe`
- Sidecar paths:
  - Windows: `third_party/tokei/win-x64/tokei.exe`
  - macOS ARM64 slot: `third_party/tokei/osx-arm64/`
- Sidecar deployment note:
  - publish output уже копирует `third_party/` рядом с приложением, поэтому `ProcessTokeiRunner` на Windows подхватывает sidecar без настройки `PATH`.

## Актуальные ограничения
- Ignore parser покрывает MVP-поднабор правил, а не всю специфику Git ignore edge-cases.
- Cache пока только in-memory и живёт в пределах процесса.
- `tokei` sidecar физически добавлен только для `win-x64`; для `osx-arm64` подготовлен слот размещения.
- Tooltip у treemap пока минималистичный и без отдельного кастомного popup layout.
- `restore/build` сейчас предупреждают о транзитивной уязвимости `Microsoft.Bcl.Memory 9.0.4` из цепочки `Microsoft.ML.Tokenizers` (`NU1903`).
- Linux support, installer/signing, single-file publish и Native AOT не входят в MVP.

## Handoff summary
- Windows-first MVP собран, тесты зелёные, `win-x64` publish и launch smoke пройдены.
- Для продолжения разработки достаточно опираться на:
  - [docs/status.md](/Z:/Projects/My/tokenmap/src/docs/status.md) - текущее состояние, команды запуска и реальные ограничения;
  - [docs/post-mvp.md](/Z:/Projects/My/tokenmap/src/docs/post-mvp.md) - backlog следующей волны;
  - [docs/spec.md](/Z:/Projects/My/tokenmap/src/docs/spec.md) и [docs/architecture.md](/Z:/Projects/My/tokenmap/src/docs/architecture.md) - продуктовые и технические инварианты.
