# TokenMap - Status

## Текущее состояние
- Статус проекта: **Этап 0 завершён**
- Цель текущего цикла: **Этап 1 - Core contracts и scanner skeleton**
- Основная платформа MVP: **Windows**
- Вторичная платформа MVP: **macOS**
- Linux: **post-MVP / not blocking**

## Этапы
- [x] Этап 0 - Bootstrap solution
- [ ] Этап 1 - Core contracts и scanner skeleton
- [ ] Этап 2 - Ignore / exclude policy
- [ ] Этап 3 - Token counting и Tokei integration
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

## Известные ограничения на текущем этапе
- UI пока не выполняет анализ и не читает файловую систему.
- Tree, treemap и details panel пока представлены layout-заглушками.
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
