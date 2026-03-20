# TokenMap - Status

## Текущее состояние
- Статус проекта: **Этап 3 завершён**
- Цель текущего цикла: **Этап 4 - Analyzer orchestration, progress, cancel, cache**
- Основная платформа MVP: **Windows**
- Вторичная платформа MVP: **macOS**
- Linux: **post-MVP / not blocking**

## Этапы
- [x] Этап 0 - Bootstrap solution
- [x] Этап 1 - Core contracts и scanner skeleton
- [x] Этап 2 - Ignore / exclude policy
- [x] Этап 3 - Token counting и Tokei integration
- [ ] Этап 4 - Analyzer orchestration, progress, cancel, cache
- [ ] Этап 5 - Main window, ViewModels, layout
- [ ] Этап 6 - Treemap layout engine и TreemapControl
- [ ] Этап 7 - Hover tooltip, selection sync, details panel справа
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

## Известные ограничения на текущем этапе
- Ignore parser покрывает MVP-поднабор правил, а не всю специфику Git ignore edge-cases.
- `IProjectAnalyzer`, progress batching, cache и полноценная отмена orchestration ещё не реализованы.
- `tokei` sidecar discovery уже поддержан, но сами sidecar-бинарники ещё не лежат в `third_party/`.
- `restore/build` сейчас предупреждают о транзитивной уязвимости `Microsoft.Bcl.Memory 9.0.4` из зависимости `Microsoft.ML.Tokenizers`.
- UI пока не подключён к analyzer и остаётся shell-заглушкой.
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
