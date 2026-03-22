# TokenMap — Agent Implementation Instructions

## 1. Как работать

Перед началом каждого цикла:
1. Прочитай:
   - `AGENTS.md`
   - `docs/spec.md`
   - `docs/architecture.md`
   - `docs/plan.md`
   - `docs/status.md`
2. Проверь, есть ли в `docs/plan.md` активный незавершённый MVP-этап.
3. Если активного этапа нет, работай только по явному запросу пользователя или по отдельно согласованному пункту из `docs/post-mvp.md`.
4. После изменений запусти сборку и тесты.
5. Обнови `docs/status.md`.

## 2. Главные правила
- Делай MVP, не platform vision.
- Не добавляй фичи из `docs/post-mvp.md` в текущий этап.
- Не делай unrelated refactors.
- Не меняй стек.
- Не тащи WebView, JS chart libraries или браузерный runtime.
- Не заменяй собственный treemap готовым heavy chart control.
- Не вводи плагины, DI-фреймворки, event bus и прочую инфраструктуру без необходимости.
- Не коммить без явного запроса пользователя.

## 3. Обязательные технические решения

### 3.1 UI
- Avalonia stable, non-preview.
- MVVM через `CommunityToolkit.Mvvm`.
- Layout:
  - toolbar сверху;
  - дерево слева;
  - treemap по центру;
  - details panel справа;
  - progress/status снизу или в верхней summary area.

### 3.2 Treemap
- Собственный `TreemapControl`.
- Один custom-rendered control.
- Hover и click обрабатываются внутри него.
- Выбранный элемент должен иметь persistent highlight.
- Hover tooltip должен работать без клика.

### 3.3 Метрики
- Tokens: локально через `Microsoft.ML.Tokenizers`.
- LOC/language breakdown: через `tokei`.
- Tree structure: только через наш scanner.

### 3.4 Ignore logic
- Поддержать `.gitignore`, `.ignore`, default excludes, user excludes`.
- Исключённые пути полностью отсутствуют в модели результата.

## 4. Tokei: как интегрировать
- Не угадывать JSON-схему по памяти.
- Сначала получить **реальный sample output** `tokei --files --output json` на фикстуре.
- Только после этого написать типы/парсер.
- Sidecar discovery:
  1. рядом с приложением в `third_party/tokei/<rid>/...`;
  2. fallback на `PATH`.
- При отмене анализа корректно завершать процесс `tokei`.

## 5. Tokenizer: как интегрировать
- Спрятать concrete tokenizer за `ITokenCounter`.
- Экземпляры tokenizer кэшировать по `TokenProfile`.
- Ошибка на одном файле не должна ломать весь анализ.
- Перед токенизацией нормализовать newlines к `\n`.

## 6. File scanning policy
- Не читать сразу все файлы в память.
- Не использовать `GetFiles()` для всего дерева разом.
- Строить потоковый/поштучный scan.
- Обход должен быть устойчивым к ошибкам доступа и race conditions.
- Симлинки/reparse points не разворачивать.

## 7. UI sync policy
Должен существовать единый источник текущего выбранного узла.
Минимально:
- `SelectedNode`
- `HoveredNode` (treemap only)

Правила:
- hover не меняет selected;
- click в treemap меняет selected;
- click/select в tree меняет selected;
- details panel всегда следует за selected.

## 8. Проверка качества после каждого этапа
Обязательно:
```bash
dotnet restore
dotnet build Clever.TokenMap.sln
dotnet test Clever.TokenMap.sln --no-build
```

Если добавлены UI/headless tests — запускать и их.

Если проверки падают:
- исправить;
- не завершать этап с красной сборкой.

## 9. Что обновлять в docs/status.md
После каждого заметного изменения обязательно добавить:
- дату/контекст;
- что реализовано или изменено;
- что проверено;
- какие решения приняты;
- что реально остаётся открытым.

## 10. Что делать, если есть неоднозначность
Если неоднозначность локальная и не меняет продуктовую цель:
- выбрать самый простой и прямой вариант;
- зафиксировать решение в `docs/status.md`.

Если неоднозначность меняет scope или архитектуру:
- не придумывать новую большую систему;
- выбрать минимальное решение, совместимое со spec.

## 11. Рекомендуемая форма отчёта по завершении этапа
В конце каждого цикла кратко дай:
1. что изменено;
2. какие команды проверки запущены;
3. какие файлы/модули затронуты;
4. что осталось открытым внутри этапа.

## 12. Локальные приоритеты платформ
Для MVP:
- в первую очередь Windows;
- затем macOS;
- Linux не шлифовать и не делать блокером.

Не тратить время текущих этапов на:
- Linux-specific polish;
- single-file publish;
- Native AOT;
- signing/notarization.

## 13. Запрещённые анти-паттерны
- giant god service без интерфейсов и ответственности;
- tree ↔ treemap sync через случайные глобальные состояния;
- один control на каждый treemap tile;
- прямой вызов `tokei` из UI-кода;
- смешивание file scanning, token counting и Avalonia views в одном классе;
- silent swallowing всех ошибок без trace/debug info.
