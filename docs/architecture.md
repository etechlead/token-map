# TokenMap — Architecture (MVP)

## 1. Solution layout

```text
Clever.TokenMap.sln
Directory.Packages.props
global.json
AGENTS.md
.codex/
  config.toml
docs/
  spec.md
  architecture.md
  plan.md
  implement.md
  status.md
  post-mvp.md
src/
  Clever.TokenMap.App/
  Clever.TokenMap.Controls/
  Clever.TokenMap.Core/
  Clever.TokenMap.Infrastructure/
tests/
  Clever.TokenMap.Core.Tests/
  Clever.TokenMap.HeadlessTests/
  Fixtures/
third_party/
  tokei/
    win-x64/
      tokei.exe
    osx-arm64/
      tokei
```

## 2. Responsibility split

### `Clever.TokenMap.Core`
Содержит:
- доменные модели;
- интерфейсы;
- orchestration анализа;
- агрегацию метрик;
- progress model;
- cache contracts.

Не содержит:
- Avalonia;
- конкретные UI-типы;
- прямую работу с `Process`, `FileSystemWatcher`, `Window` и т.д.

### `Clever.TokenMap.Infrastructure`
Содержит:
- реализацию обхода файловой системы;
- применение ignore/exclude правил;
- определение text/binary;
- адаптер токенизатора;
- `TokeiRunner`;
- кэш;
- нормализацию путей.

### `Clever.TokenMap.Controls`
Содержит:
- `TreemapControl`;
- layout engine treemap;
- вспомогательные render/hit-test модели.

### `Clever.TokenMap.App`
Содержит:
- окна, viewmodels, команды;
- orchestration пользовательских сценариев;
- binding к `Clever.TokenMap.Core`;
- summary/data presentation;
- взаимодействие tree ↔ treemap ↔ details.

## 3. Recommended project-level contracts

### Core models
Минимальный набор моделей:

- `ProjectSnapshot`
- `ProjectNode`
- `NodeMetrics`
- `ScanOptions`
- `TokenProfile`
- `AnalysisProgress`
- `SkippedReason`
- `LanguageSummary`
- `ExtensionSummary`

### Core interfaces
Минимальный набор интерфейсов:

- `IProjectAnalyzer`
- `IProjectScanner`
- `IPathFilter`
- `ITextFileDetector`
- `ITokenCounter`
- `ITokeiRunner`
- `ICacheStore`
- `IClock` (опционально, если нужен удобный тестируемый cache timestamping)

## 4. Main data flow

1. UI вызывает `IProjectAnalyzer.AnalyzeAsync(root, options, progress, cancellationToken)`.
2. Scanner строит дерево включённых путей.
3. Для каждого включённого файла:
   - определяется text/binary;
   - для text-файлов читается содержимое;
   - считаются токены;
   - готовится запись для merge.
4. Параллельно или следом вызывается `TokeiRunner`.
5. Результаты `tokei` маппятся к тем же относительным путям.
6. На уровне `ProjectAnalyzer` идёт merge:
   - структура дерева;
   - токены;
   - LOC/language breakdown;
   - skipped/errors.
7. Выполняется агрегация снизу вверх.
8. На выходе получается `ProjectSnapshot`.
9. UI обновляет tree, treemap, details и summary.

## 5. Truth boundaries

Чтобы избежать расхождений, обязательно соблюдать:

- Структура дерева определяется **только** нашим scanner.
- Исключённые файлы не должны внезапно появляться из `tokei`.
- `tokei` не управляет деревом; он только обогащает уже включённые узлы статистикой.
- Если `tokei` не знает файл, узел всё равно остаётся в дереве.
- Для unknown file допустимы частично заполненные LOC-метрики.

## 6. Path normalization rules

Нужно единообразно нормализовать пути:

- хранить `FullPath`;
- хранить `RelativePath` относительно root;
- для внутреннего ключа использовать `RelativePath` в унифицированной форме с `/`;
- сравнение путей должно учитывать Windows case-insensitive поведение там, где это нужно;
- merge с `tokei` делать по нормализованному относительному пути.

## 7. Text vs binary policy

MVP не требует идеального language-aware определения бинарности.

Достаточно:
- быстро проверить небольшой префикс файла;
- если файл похож на бинарный — пометить skipped/binary;
- не читать весь бинарный файл в память ради проверки.

## 8. Token counting policy

- Токены считаются только для text-файлов.
- Содержимое перед токенизацией нормализуется по newline к `\n`.
- Токенизатор завернуть за `ITokenCounter`.
- Экземпляры tokenizer кэшировать по `TokenProfile`.
- Ошибка токенизации отдельного файла не должна валить весь анализ.

## 9. Tokei integration policy

- `TokeiRunner` должен уметь:
  - найти sidecar `tokei` рядом с приложением;
  - либо использовать `PATH`, если sidecar не найден;
  - запускать процесс без shell;
  - читать stdout/stderr;
  - корректно завершать процесс при отмене.
- Парсер `tokei` должен опираться на реальный sample JSON, а не на догадки.

## 10. UI interaction model

### Selection
`SelectedNodeId`/`SelectedPath` — единый источник выбранного узла для:
- tree;
- treemap;
- details panel.

### Hover
`HoveredNodeId` существует только для treemap hover-состояния и tooltip.

### Sync rules
- click в treemap обновляет selected node;
- выбор в дереве обновляет treemap;
- details panel всегда показывает selected node;
- hover не меняет selected node.

## 11. Treemap rendering requirements

Treemap должен:
- хранить плоский список рассчитанных прямоугольников;
- делать hit testing по собственным данным;
- рендериться одним control;
- не создавать child control на каждый узел;
- уметь пересчитываться при:
  - смене snapshot;
  - смене metric;
  - resize;
  - смене selected/hovered node.

## 12. UI composition

Рекомендуемый root layout:
- `Grid`:
  - row 0: toolbar + summary
  - row 1: main content
  - row 2: status/progress
- main content:
  - column 0: tree
  - column 1: treemap
  - column 2: details

Важно:
- не помещать virtualized items в бесконечную высоту;
- не класть основные панели в `StackPanel`, если нужна виртуализация.

## 13. Testing strategy

### Unit tests
Покрыть:
- ignore/exclude;
- path normalization;
- aggregation;
- cache key generation;
- merge logic scanner + tokei;
- text/binary detection (минимально).

### Headless UI tests
Покрыть:
- открытие/привязку snapshot;
- selection sync;
- hover/click в treemap;
- details panel update.

## 14. Logging / diagnostics
MVP не требует сложной telemetry-системы.

Достаточно:
- аккуратных внутренних логов;
- отображения человеческого статуса анализа;
- накопления списка warnings/errors для debug.

## 15. Publish policy for MVP

- Основной publish-target: `win-x64`.
- Secondary target после рабочего Windows MVP: `osx-arm64`.
- `tokei` поставляется как sidecar файл.
- Single-file и Native AOT не входят в MVP.
