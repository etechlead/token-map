# TokenMap - Implementation Plan

## Статус
- MVP-этапы 0-9 завершены 2026-03-20.
- Пошаговый план MVP больше не дублируется в этом файле, чтобы не держать в документации уже выполненные задачи как активный backlog.

## Активный этап
- Незавершённых MVP-этапов нет.
- Новая работа выполняется только по явному запросу пользователя или по отдельно согласованному пункту из [docs/post-mvp.md](/Z:/Projects/My/tokenmap/src/docs/post-mvp.md).

## Что уже закрыто в MVP
- Bootstrap solution и базовый Avalonia shell.
- Core contracts, scanner и path normalization.
- `.gitignore` / `.ignore`, default excludes и user excludes.
- Подсчёт токенов через `Microsoft.ML.Tokenizers`.
- Интеграция `tokei`, merge метрик и агрегация по дереву.
- Analyzer orchestration, progress batching, cancel и in-memory cache.
- Рабочий main window с folder picker, tree, summary и details panel.
- Custom-rendered treemap без дочерних контролов на каждый tile.
- Hover tooltip, selection sync и persistent highlight.
- Windows-first publish и MVP handoff.

## Архив завершённых этапов
- Этап 0: Bootstrap solution
- Этап 1: Core contracts и scanner skeleton
- Этап 2: Ignore / exclude policy
- Этап 3: Token counting и Tokei integration
- Этап 4: Analyzer orchestration, progress, cancel, cache
- Этап 5: Main window, ViewModels, layout
- Этап 6: Treemap layout engine и TreemapControl
- Этап 7: Hover tooltip, selection sync, details panel справа
- Этап 8: Polish MVP и Windows-first publish
- Этап 9: MVP handoff

## Правила для следующего цикла
- Не возвращать в план уже завершённые MVP-пункты.
- Не расширять scope без явной необходимости.
- Для новых продуктовых задач использовать [docs/post-mvp.md](/Z:/Projects/My/tokenmap/src/docs/post-mvp.md) как backlog, а [docs/status.md](/Z:/Projects/My/tokenmap/src/docs/status.md) как источник текущего состояния.
- После каждого заметного изменения обновлять [docs/status.md](/Z:/Projects/My/tokenmap/src/docs/status.md).

## Базовые команды проверки
После каждого изменения запускать минимум:

```bash
dotnet restore
dotnet build Clever.TokenMap.sln
dotnet test Clever.TokenMap.sln --no-build
```

Если изменение затрагивает publish или headless/UI-сценарии, запускать и соответствующие дополнительные проверки.
