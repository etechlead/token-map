# TokenMap - Implementation Plan

MVP завершён. Этот файл фиксирует только текущий статус и правила следующего цикла работы.

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

## Правила для следующего цикла
- Новая работа выполняется только по явному запросу пользователя или по отдельно согласованному пункту из [docs/post-mvp.md](/Z:/Projects/My/tokenmap/src/docs/post-mvp.md).
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
