# TokenMap — Implementation Plan (MVP)

## Общие правила
- Работать **по одному этапу за раз**.
- Следующий этап начинать только после зелёной сборки и тестов предыдущего.
- После каждого этапа обновлять `docs/status.md`.
- Не расширять scope этапа без необходимости.

## Базовые команды проверки
После каждого этапа запускать минимум:

```bash
dotnet restore
dotnet build Clever.TokenMap.sln
dotnet test Clever.TokenMap.sln --no-build
```

Если этап затрагивает headless/UI-тесты — запускать их тоже.

---

## Этап 0. Bootstrap solution

### Цель
Поднять каркас solution, проекты и документацию.

### Что должно быть сделано
- Создан `Clever.TokenMap.sln`.
- Созданы проекты:
  - `src/Clever.TokenMap.App`
  - `src/Clever.TokenMap.Controls`
  - `src/Clever.TokenMap.Core`
  - `src/Clever.TokenMap.Infrastructure`
  - `tests/Clever.TokenMap.Core.Tests`
  - `tests/Clever.TokenMap.HeadlessTests`
- Подключены базовые зависимости:
  - Avalonia
  - CommunityToolkit.Mvvm
  - xUnit (или эквивалент)
  - Avalonia headless test support
- В репозитории есть:
  - `AGENTS.md`
  - `.codex/config.toml`
  - `docs/spec.md`
  - `docs/architecture.md`
  - `docs/plan.md`
  - `docs/implement.md`
  - `docs/status.md`
  - `docs/post-mvp.md`
- Приложение стартует и показывает пустой основной layout.

### Acceptance criteria
- `dotnet build` проходит.
- `dotnet test` проходит.
- Есть окно с layout: toolbar сверху, дерево слева, treemap placeholder по центру, details placeholder справа.

---

## Этап 1. Core contracts и scanner skeleton

### Цель
Создать доменные модели, интерфейсы и базовый обход проекта.

### Что должно быть сделано
- Добавлены core models:
  - `ProjectSnapshot`
  - `ProjectNode`
  - `NodeMetrics`
  - `ScanOptions`
  - `TokenProfile`
  - `SkippedReason`
  - `AnalysisProgress`
- Добавлены interfaces:
  - `IProjectAnalyzer`
  - `IProjectScanner`
  - `IPathFilter`
  - `ITextFileDetector`
  - `ITokenCounter`
  - `ITokeiRunner`
  - `ICacheStore`
- Реализован scanner, который:
  - обходит каталоги;
  - пропускает symlink/reparse points;
  - устойчив к ошибкам доступа;
  - строит дерево включённых путей.
- Добавлена path normalization strategy.
- Добавлены unit tests на path normalization и базовый scanner.

### Acceptance criteria
- Scanner возвращает детерминированную структуру дерева.
- Ошибки отдельных файлов/папок не валят scan.
- Тесты на path normalization и basic scan проходят.

---

## Этап 2. Ignore / exclude policy

### Цель
Реально включить `.gitignore`, `.ignore`, дефолтные excludes и пользовательские excludes.

### Что должно быть сделано
- Подключён parser ignore-правил.
- Реализована логика:
  - root и nested `.gitignore`;
  - root и nested `.ignore`;
  - дефолтные excludes;
  - пользовательские excludes.
- Исключённые пути не попадают в дерево.
- Добавлены fixture-проекты для тестов ignore-логики.

### Acceptance criteria
- Фикстура с `node_modules`, `bin`, `obj`, `.git` реально скрывается.
- Nested ignore rules работают в пределах поддерева.
- User excludes работают относительно root.
- Тесты ignore/exclude проходят.

---

## Этап 3. Token counting и Tokei integration

### Цель
Добавить реальные метрики по файлам.

### Что должно быть сделано
- Реализован `ITokenCounter` через `Microsoft.ML.Tokenizers`.
- Поддержаны профили:
  - `o200k_base`
  - `cl100k_base`
  - `p50k_base`
- Реализован `ITextFileDetector`.
- Реализован `ITokeiRunner`:
  - поиск sidecar;
  - fallback на `PATH`;
  - запуск процесса;
  - парсинг JSON output.
- Реализован merge:
  - scanner tree
  - tokens
  - tokei stats
- Для unknown/unsupported файлов допускается частичное заполнение метрик.
- Добавлены unit/integration tests на merge и частичные данные.

### Acceptance criteria
- Tokens считаются для text-файлов.
- Binary файлы не валят анализ.
- Для поддерживаемых Tokei файлов есть `Language`, `CodeLines`, `CommentLines`, `BlankLines`.
- Для папок метрики агрегируются корректно.
- Тесты на token/tokei merge проходят.

---

## Этап 4. Analyzer orchestration, progress, cancel, cache

### Цель
Сделать полноценный анализатор, пригодный для UI.

### Что должно быть сделано
- Реализован `IProjectAnalyzer`.
- Поддержан `CancellationToken`.
- Добавлен progress reporting батчами.
- Реализован простой кэш по:
  - path
  - size
  - last write time UTC
  - token profile
- Добавлены tests на:
  - cancel;
  - cache hit/miss;
  - aggregation correctness.

### Acceptance criteria
- Повторный scan на том же проекте использует кэш.
- Cancel реально прерывает анализ.
- Snapshot после отмены не ломает приложение.
- Тесты orchestration/cancel/cache проходят.

---

## Этап 5. Main window, ViewModels, layout

### Цель
Подключить реальный UI-каркас к analyzer.

### Что должно быть сделано
- Реализованы ViewModels для:
  - main window;
  - toolbar;
  - tree;
  - details panel;
  - summary/progress.
- UI умеет:
  - выбрать папку;
  - запустить анализ;
  - отменить анализ;
  - показать summary;
  - показать дерево слева;
  - показать details placeholder справа.
- Состояния `Idle / Scanning / Completed / Cancelled / Failed` отражаются в UI.
- Добавлены headless tests на базовые VM/UI сценарии.

### Acceptance criteria
- Можно выбрать папку и получить заполненное дерево.
- Summary обновляется после анализа.
- Details panel обновляется при выборе узла в дереве.
- Headless tests на базовую загрузку snapshot проходят.

---

## Этап 6. Treemap layout engine и TreemapControl

### Цель
Реализовать быстрый treemap без лишних visual elements.

### Что должно быть сделано
- Создан `SquarifiedTreemapLayout`.
- Создан `TreemapControl`.
- Control:
  - принимает snapshot/subtree;
  - принимает выбранную метрику;
  - рассчитывает прямоугольники;
  - рисует их через custom rendering;
  - поддерживает hover и click hit testing.
- Добавлены модели:
  - `TreemapRect`
  - `TreemapNodeVisual` или эквивалент.
- Добавлены unit tests на layout.
- Добавлены headless/visual tests на базовый render.

### Acceptance criteria
- Treemap отображает структуру узлов.
- Прямоугольники корректно масштабируются по метрике.
- Нет child controls на каждый узел.
- Тесты layout/render проходят.

---

## Этап 7. Hover tooltip, selection sync, details panel справа

### Цель
Довести UX treemap до целевого MVP.

### Что должно быть сделано
- Hover по прямоугольнику:
  - подсвечивает узел;
  - показывает tooltip/popup.
- Click по прямоугольнику:
  - выделяет узел;
  - включает persistent highlight;
  - синхронизирует выбор с деревом;
  - обновляет details panel справа.
- Выбор в дереве синхронизирует treemap.
- Details panel показывает полную статистику выбранного узла.
- Добавлены headless tests на selection sync и tooltip state.

### Acceptance criteria
- Hover работает без клика.
- Selected node в treemap визуально отличается от hovered.
- Tree ↔ treemap ↔ details синхронизированы.
- Headless tests на UX-сценарии проходят.

---

## Этап 8. Polish MVP и Windows-first publish

### Цель
Довести MVP до состояния, пригодного для использования и передачи на ручное тестирование.

### Что должно быть сделано
- Убраны грубые UI-шероховатости.
- Добавлены summary cards/summary strip.
- Добавлены понятные ошибки/status messages.
- Приведены в порядок иконки/подписи/плейсхолдеры.
- Настроен publish под `win-x64`.
- Подготовлена структура для sidecar `tokei`.
- Добавлены инструкции в `docs/status.md` по запуску MVP.
- Если возможно без лишнего scope — подготовлен secondary publish smoke target для `osx-arm64` без обязательной ручной проверки на этом этапе.

### Acceptance criteria
- Windows publish получается и запускается локально.
- `tokei` корректно обнаруживается как sidecar.
- UI выглядит цельно и не разваливается на основных сценариях.
- Сборка и тесты зелёные.
- `docs/status.md` содержит финальное состояние MVP.

---

## Этап 9. MVP handoff

### Цель
Подготовить репозиторий к передаче человеку и следующему циклу работы.

### Что должно быть сделано
- Проверены документы на актуальность.
- Обновлён `docs/status.md`.
- Зафиксирован список post-MVP задач.
- Сформулированы known issues/ограничения MVP.
- Указан способ запуска приложения и размещения `tokei`.

### Acceptance criteria
- Репозиторий можно открыть и продолжить разработку без устных пояснений.
- Документация соответствует фактическому состоянию кода.
