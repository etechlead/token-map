# TokenMap - Status

## Текущее состояние
- Статус проекта: **Этап 7 завершён**
- Цель текущего цикла: **Этап 8 - Polish MVP и Windows-first publish**
- Основная платформа MVP: **Windows**
- Вторичная платформа MVP: **macOS**
- Linux: **post-MVP / not blocking**

## Этапы
- [x] Этап 0 - Bootstrap solution
- [x] Этап 1 - Core contracts и scanner skeleton
- [x] Этап 2 - Ignore / exclude policy
- [x] Этап 3 - Token counting и Tokei integration
- [x] Этап 4 - Analyzer orchestration, progress, cancel, cache
- [x] Этап 5 - Main window, ViewModels, layout
- [x] Этап 6 - Treemap layout engine и TreemapControl
- [x] Этап 7 - Hover tooltip, selection sync, details panel справа
- [ ] Этап 8 - Polish MVP и Windows-first publish
- [ ] Этап 9 - MVP handoff

## Зафиксированные решения
- Стек MVP: `.NET 10 + Avalonia stable + CommunityToolkit.Mvvm`.
- Версии пакетов централизованы через `Directory.Packages.props`.
- SDK закреплён через `global.json` на `10.0.201`.
- Репозиторий очищен от build-артефактов через `.gitignore`.
- Tokei используется в MVP как sidecar.
- Treemap реализуется собственным custom control.
- UI layout: toolbar сверху, tree слева, treemap по центру, details справа.
- Hover tooltip обязателен.
- Selected tile в treemap должен иметь persistent highlight.
- Основной фокус MVP - Windows.
- macOS тестируется вторым приоритетом.
- Single-file и Native AOT вынесены в post-MVP.
- Linux не блокирует MVP.

## Журнал выполнения

### 2026-03-20
- Этап: **Этап 0 - Bootstrap solution**
- Что сделано:
  - создан `Clever.TokenMap.sln`;
  - созданы проекты `Clever.TokenMap.App`, `Clever.TokenMap.Controls`, `Clever.TokenMap.Core`, `Clever.TokenMap.Infrastructure`, `Clever.TokenMap.Core.Tests`, `Clever.TokenMap.HeadlessTests`;
  - добавлены `Directory.Packages.props`, `global.json`, `tests/Fixtures/.gitkeep`;
  - Avalonia shell поднят на `net10.0` с базовым layout: toolbar сверху, tree placeholder слева, treemap placeholder по центру, details placeholder справа, status strip снизу;
  - подключены проектные ссылки между слоями;
  - добавлены bootstrap smoke-tests: unit test для core assembly и headless test на состав главного окна.
- Что проверено:
  - `dotnet restore`
  - `dotnet build Clever.TokenMap.sln`
  - `dotnet test Clever.TokenMap.sln --no-build`
- Принятые решения:
  - для bootstrap-этапа treemap и details оставлены как placeholders без ранней бизнес-логики;
  - headless UI smoke-test проверяет наличие ключевых секций окна по `Name`, чтобы зафиксировать shell перед дальнейшими этапами.
- Открытые вопросы / Отложено:
  - реальные domain models, scanner и orchestration перенесены в Этап 1;
  - folder picker, rescan/cancel logic и рабочие VM-сценарии остаются на последующих этапах по плану.

### 2026-03-20
- Этап: **Этап 1 - Core contracts и scanner skeleton**
- Что сделано:
  - добавлены core models `ProjectSnapshot`, `ProjectNode`, `NodeMetrics`, `ScanOptions`, `TokenProfile`, `SkippedReason`, `AnalysisProgress` и базовая модель `TokeiFileStats`;
  - добавлены интерфейсы `IProjectAnalyzer`, `IProjectScanner`, `IPathFilter`, `ITextFileDetector`, `ITokenCounter`, `ITokeiRunner`, `ICacheStore`;
  - реализована стратегия нормализации путей через `PathNormalizer`;
  - реализован `FileSystemProjectScanner` с детерминированной сортировкой, обходом каталогов, пропуском reparse points и мягкой обработкой ошибок доступа;
  - добавлен `AllowAllPathFilter` как текущая baseline-реализация фильтра;
  - добавлены unit tests на нормализацию путей и базовый scanner;
  - репозиторий очищен от отслеживания `bin/obj` через `.gitignore`.
- Что проверено:
  - `dotnet restore`
  - `dotnet build Clever.TokenMap.sln`
  - `dotnet test Clever.TokenMap.sln --no-build`
- Принятые решения:
  - scanner на этом этапе возвращает `ProjectSnapshot` уже на уровне структуры дерева, а метрики пока остаются пустыми;
  - recoverable file system errors не валят scan: путь помечается skipped и сообщение уходит в warnings;
  - относительные пути нормализуются к виду с `/`, а сравнение путей на Windows идёт case-insensitive.
- Открытые вопросы / Отложено:
  - полноценная ignore/exclude policy остаётся на Этап 2;
  - реальные токены, text/binary detection и интеграция с `tokei` остаются на Этап 3;
  - orchestration analyzer и cache остаются на Этап 4.

### 2026-03-20
- Этап: **Этап 2 - Ignore / exclude policy**
- Что сделано:
  - добавлен parser/evaluator ignore-правил для `.gitignore` и `.ignore`;
  - `FileSystemProjectScanner` теперь учитывает root и nested ignore-файлы в пределах поддерева;
  - добавлены default excludes для `.git`, `.vs`, `.idea`, `.vscode`, `node_modules`, `bin`, `obj`, `dist`, `build`, `out`, `coverage`, `target`, `Debug`, `Release`;
  - добавлена обработка user excludes относительно root через `ScanOptions.UserExcludes`;
  - исключённые пути больше не попадают в дерево snapshot;
  - добавлен fixture-проект `tests/Fixtures/IgnorePolicyFixture` и тесты на default excludes, nested `.gitignore`/`.ignore`, toggles и user excludes.
- Что проверено:
  - `dotnet restore`
  - `dotnet build Clever.TokenMap.sln`
  - `dotnet test Clever.TokenMap.sln --no-build`
- Принятые решения:
  - default excludes реализованы как жёсткие directory-name rules, применяемые рекурсивно;
  - user excludes трактуются как root-relative patterns и не расширяются на одноимённые вложенные пути;
  - nested ignore rules применяются только к своему поддереву через контекст правил, который накапливается во время обхода scanner.
- Открытые вопросы / Отложено:
  - полная gitignore-совместимость за пределами MVP-необходимого поднабора правил не добавлялась;
  - метрики токенов и LOC остаются на следующем этапе.

### 2026-03-20
- Этап: **Этап 3 - Token counting и Tokei integration**
- Что сделано:
  - подключён `Microsoft.ML.Tokenizers` и реализован `MicrosoftMlTokenCounter` с поддержкой профилей `o200k_base`, `cl100k_base`, `p50k_base`;
  - реализован `HeuristicTextFileDetector`, который проверяет небольшой префикс файла и помечает binary-файлы без чтения всего содержимого в память;
  - реализован `ProcessTokeiRunner` с поиском sidecar `third_party/tokei/<rid>/...`, fallback на `PATH`, запуском процесса без shell, корректным завершением по cancel и парсингом JSON через `TokeiJsonParser`;
  - на базе реального sample output `tokei` добавлен merge scanner tree + token metrics + tokei stats в `ProjectSnapshotMetricsEnricher`;
  - для text-файлов считается `Tokens`, при отсутствии статистики `tokei` локально считается `TotalLines`, а `CodeLines/CommentLines/BlankLines/Language` остаются частично заполненными;
  - для папок добавлена bottom-up агрегация `Tokens`, LOC, размера и счётчиков включённых файлов/каталогов;
  - добавлены тесты на token profiles, text/binary heuristic, `tokei` JSON parser, merge/aggregation и деградацию при исчезновении файла во время анализа;
  - `.gitignore` дополнен правилом для `.idea/`, чтобы локальные IDE-артефакты не засоряли рабочее дерево.
- Что проверено:
  - `dotnet restore`
  - `dotnet build Clever.TokenMap.sln`
  - `dotnet test Clever.TokenMap.sln --no-build`
- Принятые решения:
  - merge и агрегация вынесены в отдельный `ProjectSnapshotMetricsEnricher`, который можно напрямую переиспользовать в `IProjectAnalyzer` на следующем этапе;
  - если `tokei` недоступен или падает, анализ не валится целиком: snapshot получает warning и остаётся с токенами и частичными LOC-метриками;
  - binary-файлы сохраняются в дереве и участвуют в aggregate по размеру/счётчикам, но не получают токены и LOC.
- Открытые вопросы / Отложено:
  - sidecar-бинарники `tokei` ещё не добавлены в репозиторий; на текущем этапе реализован discovery, а поставка sidecar остаётся на publish/polish-этап;
  - `Microsoft.ML.Tokenizers` тянет транзитивный `Microsoft.Bcl.Memory 9.0.4`, поэтому `restore/build` сейчас дают warning `NU1903`; это зафиксировано отдельно и не блокирует MVP-этап 3;
  - orchestration через `IProjectAnalyzer`, progress batching, cancel и cache остаются на Этап 4.

### 2026-03-20
- Этап: **Этап 4 - Analyzer orchestration, progress, cancel, cache**
- Что сделано:
  - реализован `ProjectAnalyzer`, который оркестрирует scanner + metrics enrichment и предоставляет рабочую реализацию `IProjectAnalyzer`;
  - добавлен `BufferedAnalysisProgress` для пакетной отправки progress вместо обновления UI на каждый узел;
  - добавлен `InMemoryCacheStore` с ключом по `fullPath + fileSizeBytes + lastWriteTimeUtc + tokenProfile`;
  - `ProjectSnapshotMetricsEnricher` интегрирован с cache: при cache hit повторно не читает файл и не вызывает token counter;
  - cancel-path протянут через analyzer, scanner, enrichment и token/tokei layer без возврата битого partial snapshot;
  - добавлены tests на cache hit, cache miss после изменения файла, batched progress и отмену во время metrics stage.
- Что проверено:
  - `dotnet restore`
  - `dotnet build Clever.TokenMap.sln`
  - `dotnet test Clever.TokenMap.sln --no-build`
- Принятые решения:
  - cache оставлен in-memory и process-local, без дискового формата, чтобы не раздувать MVP и не усложнять invalidation;
  - progress batching реализован простым буфером по количеству событий с flush на смене phase и на terminal progress;
  - analyzer остаётся в infrastructure-слое как concrete orchestration поверх core contracts, без DI-фреймворка и без раннего усложнения композиции.
- Открытые вопросы / Отложено:
  - persist cache на диск не добавлялся и остаётся вне scope MVP-необходимого этапа;
  - UI пока не подключён к analyzer, progress и cancel-кнопкам; это остаётся на Этап 5;
  - предупреждение `NU1903` по транзитивному `Microsoft.Bcl.Memory 9.0.4` остаётся актуальным и зафиксировано отдельно.

### 2026-03-20
- Этап: **Этап 5 - Main window, ViewModels, layout**
- Что сделано:
  - `MainWindowViewModel` больше не является shell-заглушкой и теперь оркестрирует folder pick, analyzer run, cancel и state transitions;
  - добавлены отдельные ViewModel-слои `ToolbarViewModel`, `ProjectTreeViewModel`, `ProjectTreeNodeViewModel`, `DetailsPanelViewModel`, `SummaryViewModel`;
  - `App.axaml.cs` теперь создаёт реальный `ProjectAnalyzer` с scanner/token/tokei/cache и прокидывает его в UI;
  - добавлен app-level `IFolderPickerService` и concrete `WindowFolderPickerService` на базе Avalonia folder picker;
  - `MainWindow.axaml` переведён с placeholder bindings на рабочие команды и данные: `Open Folder`, `Rescan`, `Cancel`, tree binding, details binding, summary/progress/status binding;
  - в UI отражаются состояния `Idle / Scanning / Completed / Cancelled / Failed`;
  - добавлены headless tests на open-folder flow и cancel flow поверх реального окна.
- Что проверено:
  - `dotnet restore`
  - `dotnet build Clever.TokenMap.sln`
  - `dotnet test Clever.TokenMap.sln --no-build`
- Принятые решения:
  - folder picker оставлен в app-слое через сервис, чтобы UI не зависел от конкретного окна внутри ViewModel;
  - после cancel/fail предыдущее дерево не очищается, чтобы UI не оставался в битом состоянии;
  - tree selection уже синхронизирована с details panel, а treemap пока остаётся placeholder до следующего этапа.
- Открытые вопросы / Отложено:
  - treemap в центре всё ещё placeholder и будет заменён реальным control на Этапе 6;
  - полноценная selection sync tree ↔ treemap ↔ details закрывается только после появления treemap;
  - warning `NU1903` по транзитивному `Microsoft.Bcl.Memory 9.0.4` остаётся актуальным.

### 2026-03-20
- Этап: **Этап 6 - Treemap layout engine и TreemapControl**
- Что сделано:
  - добавлен `Clever.TokenMap.Controls` → `Clever.TokenMap.Core` reference, чтобы treemap работал напрямую с `ProjectNode`;
  - реализован `SquarifiedTreemapLayout`, который раскладывает узлы по выбранной метрике `Tokens / Total lines / Code lines` и строит плоский набор `TreemapNodeVisual`;
  - реализован `TreemapControl` с custom rendering без дочерних контролов на каждый прямоугольник;
  - treemap интегрирован в `MainWindow.axaml`, а `MainWindowViewModel` теперь прокидывает в него `TreemapRootNode`;
  - control считает layout на arrange/property change, рендерит placeholder для пустого состояния и поддерживает базовый hit testing через `HitTestNode`, `HoveredNode` и `PressedNode`;
  - добавлены unit tests на границы layout, отсутствие overlap у верхнего уровня и масштабирование по выбранной метрике;
  - добавлены headless tests на присутствие treemap в окне, базовый render smoke и hit testing по нарисованному прямоугольнику.
- Что проверено:
  - `dotnet restore`
  - `dotnet build Clever.TokenMap.sln`
  - `dotnet test Clever.TokenMap.sln --no-build`
- Принятые решения:
  - layout возвращает плоский набор visual-моделей, а не отдельное visual tree, чтобы treemap оставался дешёвым для больших snapshot;
  - для hit testing выбирается самый глубокий прямоугольник под точкой, потому что список visual-узлов строится в порядке parent → children;
  - визуальные hover/selection state и синхронизация с остальным UI сознательно отложены на следующий этап, но механика hit testing уже подготовлена.
- Открытые вопросы / Отложено:
  - tooltip, persistent highlight и tree ↔ treemap ↔ details sync остаются на Этапе 7;
  - styling treemap пока утилитарный и будет дошлифован на последующих этапах;
  - warning `NU1903` по транзитивному `Microsoft.Bcl.Memory 9.0.4` остаётся актуальным.

### 2026-03-20
- Этап: **Этап 7 - Hover tooltip, selection sync, details panel справа**
- Что сделано:
  - `MainWindowViewModel` теперь хранит единый `SelectedNode`, который используется как источник истины для tree, treemap и details;
  - `ProjectTreeViewModel` получил индекс по `ProjectNode.Id` и умеет выбирать узел по id для обратной синхронизации из treemap;
  - `TreemapControl` получил two-way `SelectedNode`, hover state, tooltip content, distinct hover/selected borders и selection-by-hit-test;
  - details panel отвязан от tree-specific VM и теперь строится напрямую от `ProjectNode` с полным набором MVP-метрик: path, kind, tokens, lines, code/comments/blanks, language, extension, size, descendants, share и top children по текущей метрике;
  - metric switch в toolbar теперь обновляет details panel для уже выбранного узла без повторного анализа;
  - добавлены headless tests на treemap → tree/details sync, tree → treemap/details sync и tooltip hover state.
- Что проверено:
  - `dotnet restore`
  - `dotnet build Clever.TokenMap.sln`
  - `dotnet test Clever.TokenMap.sln --no-build`
- Принятые решения:
  - hover остаётся локальным состоянием treemap, а глобально синхронизируется только selected node;
  - selection sync строится по `ProjectNode.Id`, а не по строковым путям из UI, чтобы tree и treemap ссылались на один и тот же snapshot;
  - details panel считает share и top children относительно текущей выбранной treemap-метрики, а не только по токенам.
- Открытые вопросы / Отложено:
  - tooltip styling пока текстовый и минималистичный, без отдельного кастомного popup layout;
  - раскрытие пути к выбранному treemap-узлу в `TreeView` специально не усложнялось и остаётся потенциальной polish-задачей;
  - warning `NU1903` по транзитивному `Microsoft.Bcl.Memory 9.0.4` остаётся актуальным.

## Известные ограничения на текущем этапе
- Ignore parser покрывает MVP-поднабор правил, а не всю специфику Git ignore edge-cases.
- Cache пока только in-memory и живёт в пределах процесса.
- `tokei` sidecar discovery уже поддержан, но сами sidecar-бинарники ещё не лежат в `third_party/`.
- `restore/build` сейчас предупреждают о транзитивной уязвимости `Microsoft.Bcl.Memory 9.0.4` из зависимости `Microsoft.ML.Tokenizers`.
- Tooltip у treemap пока минималистичный и не оформлен отдельным кастомным popup.
- Linux support не доводится в MVP.
- Installer/signing не входят в MVP.
- Single-file publish не входит в MVP.
- Native AOT не входит в MVP.

## Напоминание агенту
Перед началом следующего цикла:
1. прочитать `AGENTS.md` и документы в `docs/`;
2. выполнять только следующий незавершённый этап;
3. после изменений прогонять build/tests;
4. обновлять этот файл.
