# TokenMap — Post-MVP Plan

Этот документ описывает задачи, которые **не входят в MVP**, но заранее запланированы после завершения рабочего MVP.

## 1. Ближайший post-MVP

### 1.1 macOS polish
- регулярная ручная проверка на macOS;
- улучшение упаковки и запуска;
- исправление platform-specific UI/FS шероховатостей.

### 1.2 Linux support
- целевая проверка на Linux;
- шлифовка X11/XWayland поведения;
- packaging strategy для Linux;
- проверка работы `tokei` sidecar и зависимостей в Linux-среде.

### 1.3 Publish improvements
- single-file publish;
- уменьшение размера артефактов;
- улучшение структуры release artifacts;
- автоматизация packaging.

### 1.4 Native AOT
- исследование применимости Native AOT;
- trimming/AOT-safe corrections;
- профилирование startup и memory footprint;
- включение AOT только после стабильного non-AOT baseline.

## 2. Следующая продуктовая волна

### 2.1 Snapshot export / import
- сохранение результатов анализа в JSON;
- повторное открытие snapshot без пересканирования;
- воспроизводимые сравнения между машинами/ветками.

### 2.2 Compare / Diff
- сравнение двух snapshot;
- изменения по tokens / LOC;
- добавленные/удалённые/изменённые области treemap.

### 2.3 Search / filter UI
- фильтр по path;
- фильтр по extension;
- фильтр по language;
- quick search по дереву.

### 2.4 Treemap navigation improvements
- zoom into folder;
- breadcrumb navigation;
- pan/zoom;
- back/forward по истории выбора.

### 2.5 Live watch mode
- перескан по изменениям файлов;
- debounce/coalescing событий;
- инкрементальное обновление snapshot.

## 3. Более продвинутые функции

### 3.1 Дополнительные режимы анализа
- метрика по bytes;
- количество файлов как отдельная метрика;
- top-N отчёты;
- пер-расширение/пер-язык breakdown views.

### 3.2 Настройки
- сохранение последних проектов;
- сохранение пользовательских excludes;
- пользовательские пресеты tokenizer profile;
- ограничение по максимальному размеру файла;
- тонкая настройка text/binary detection.

### 3.3 Улучшение tree/details UX
- multi-column tree;
- richer details panel;
- открытие файла/папки в системном explorer;
- quick copy path.

### 3.4 Экспорт и отчёты
- CSV/JSON экспорт summary;
- markdown report;
- shareable snapshot bundle.

## 4. Engineering backlog

### 4.1 CI/CD
- полноценная матрица publish под несколько ОС;
- артефакты для релизов;
- smoke tests на нескольких runtime.

### 4.2 Installer / signing
- Windows installer;
- macOS signing/notarization;
- Linux packaging strategy.

### 4.3 Diagnostics
- более удобный debug log;
- performance trace mode;
- внутренние dev overlays для treemap/layout profiling.

## 5. Что сознательно не тянуть в MVP
Ниже перечислено то, что не должно "просачиваться" в MVP без отдельного решения:
- Linux polish;
- single-file;
- Native AOT;
- export/import;
- diff;
- live watch;
- zoom/pan treemap;
- installers;
- signing/notarization;
- сложная система настроек.

## 6. Правило для агента
Если фича относится к этому документу, а не к `docs/spec.md`, она **не является обязательной частью текущего MVP** и не должна добавляться без явного запроса.
