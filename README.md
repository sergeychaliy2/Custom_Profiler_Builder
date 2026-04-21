# NexusBuildPro — Custom Profiler Builder

**Editor-расширение для Unity**, разворачивающее в одно окно полный жизненный цикл
кросс-платформенной сборки: конфигурация профилей → валидация → оптимизация ассетов →
вызов `BuildPipeline` → профилирование шагов → история и аналитика.

> Не путать с runtime-профайлером геймплея. Это **инструмент для build engineer-а**:
> профилируется сам процесс сборки (длительность шагов, trend, распределение по
> платформам), а не фреймрейт игры.

https://github.com/user-attachments/assets/2b4e9a5a-2574-4fb0-92c0-811acd8e1c08

---

## TL;DR

- Одно `EditorWindow` с 5-ю вкладками: **Dashboard / Platforms / Optimizer / Profiler / History**.
- **Strategy + Pipeline** архитектура: добавить платформу = унаследовать один класс, добавить шаг сборки = реализовать один интерфейс. Регистрация автоматическая через `TypeCache`.
- Профилирование с разбивкой по шагам, Gantt-таймлайном, bar/line/pie-чартами (GL-примитивы, без сторонних либ).
- История сборок в `EditorPrefs` (до 100 последних), incremental-кэш манифеста через MD5.
- **7 платформ** из коробки: Windows / Linux / macOS / Android / iOS / WebGL.
- Весь код в отдельной Editor-сборке (`NexusBuildPro.Editor.asmdef`), не влияет на runtime.

---

## Стек и требования

| Компонент | Версия/Примечание |
|---|---|
| Unity Editor | 6000+ (используется C# 10 `[..8]`-синтаксис, target-typed `new()`) |
| .NET | .NET Standard 2.1 / API совместимый |
| Сборка | `Assets/NexusBuildPro/Editor/NexusBuildPro.Editor.asmdef` (Editor-only) |
| Render | IMGUI + `GL.*` для чартов (шейдер `Hidden/Internal-Colored`) |
| Внешние зависимости | **Нет** — только UnityEditor API |

---

## Быстрый старт

1. **Открыть окно**: `Tools → NexusBuildPro → Open NexusBuildPro` или `Ctrl + Shift + B`.
2. **Создать профиль**: сайдбар → `+ New Build Profile`. Появится `ScriptableObject` типа `BuildProfile`.
3. **Выбрать платформу**: вкладка **Platforms** → клик по тайлу нужной платформы.
4. **Нажать `▶ BUILD NOW`**. Прогресс, лог и метрики — в реальном времени.
5. **Quick Build**: `Ctrl + Shift + Alt + B` — собрать активный профиль без заходов в окно.

---

## Архитектура

### Слоистая структура

```
┌─────────────────────────────────────────────────────────────┐
│ UI Layer (IMGUI)                                             │
│   NexusBuildWindow ── orchestrates ──┐                       │
│   Views/*          (Dashboard, Platforms, Optimizer,         │
│                     Profiler, History)                       │
│   ChartRenderer    (GL primitives)                           │
│   NexusStyles      (centralized theming + tex cache)         │
└────────────────────────┬────────────────────────────────────┘
                         │ events (Observer)
┌────────────────────────▼────────────────────────────────────┐
│ Orchestration Layer                                          │
│   BuildOrchestrator  — facade, событийный конвейер           │
│   BuildContext       — state-bag на время одной сборки       │
│   BuildProfile (SO)  — декларативная конфигурация            │
│   BuildResult        — immutable снимок результата           │
└──┬──────────────┬─────────────────┬────────────────┬────────┘
   │              │                 │                │
┌──▼──────┐  ┌────▼──────┐  ┌───────▼──────┐  ┌──────▼─────┐
│Strategies│  │  Steps    │  │  Profiling   │  │    Cache   │
│(per-     │  │(pipeline) │  │  Metrics +   │  │  Manifest+ │
│ platform)│  │           │  │  Sessions    │  │  MD5 hash  │
└──────────┘  └───────────┘  └──────────────┘  └────────────┘
                         │
                         ▼
                  Unity BuildPipeline
```

### Ключевые контракты

| Интерфейс | Файл | Назначение |
|---|---|---|
| `IPlatformBuildStrategy` | [IPlatformBuildStrategy.cs](Assets/NexusBuildPro/Editor/Core/Interfaces/IPlatformBuildStrategy.cs) | Абстракция платформы: `ConfigureBuildOptions`, `PreparePlatform`, `CleanupPlatform`, `ValidateConfiguration` |
| `IBuildStep` | [IBuildStep.cs](Assets/NexusBuildPro/Editor/Core/Interfaces/IBuildStep.cs) | Атомарный шаг пайплайна (`Order`, `IsEnabled`, `ExecuteAsync`) |
| `IBuildOptimizer` | [IBuildOptimizer.cs](Assets/NexusBuildPro/Editor/Core/Interfaces/IBuildOptimizer.cs) | Оптимизатор ассетов (зарезервировано под будущую плагинную систему) |

### Поток выполнения одной сборки

```
User click ▶
    │
NexusBuildWindow.TriggerBuild()
    │ EditorApplication.delayCall  ← выход из OnGUI в безопасный момент
    ▼
BuildOrchestrator.BuildAsync(profile, strategy)
    │
    ├─ _profiler.Begin(sessionId)
    ├─ _cache.BeginSession()
    ├─ strategy.IsModuleInstalled?         ─── no ──► BuildFailed
    ├─ strategy.ValidateConfiguration()    ─── err ─► BuildFailed
    ├─ PrepareOutputDirectory()
    │
    ├─ RunPipelineStepsSync (порядок по IBuildStep.Order):
    │     10 ─ SceneValidationStep
    │     20 ─ AssetOptimizationStep
    │    200 ─ PostBuildStep
    │
    ├─ strategy.PreparePlatform(ctx)
    ├─ strategy.ConfigureBuildOptions(ctx)
    ├─ BuildPipeline.BuildPlayer(options)  ◄── синхронный Unity-вызов
    ├─ strategy.CleanupPlatform(ctx)
    ├─ CalculateOutputSize()
    ├─ RecordHistory() → EditorPrefs
    ├─ _cache.CommitSession() → Library/NexusBuildPro/Cache
    └─ OnBuildCompleted?.Invoke(result)
```

**Важная тонкость async/sync:** метод объявлен `async Task<BuildResult>`, но до
`BuildPipeline.BuildPlayer` нет ни одного `await` (шаги прогоняются через
`GetAwaiter().GetResult()`). Это намеренно: Unity 6 валит `BuildPlayer`, если
цепочка вызова была прервана SynchronizationContext-ом из async-пост-обработки.
Детали — в комментариях [BuildOrchestrator.cs:112](Assets/NexusBuildPro/Editor/Core/BuildOrchestrator.cs:112) и
[BuildOrchestrator.cs:204](Assets/NexusBuildPro/Editor/Core/BuildOrchestrator.cs:204).

---

## Применённые паттерны

| Паттерн | Где | Комментарий |
|---|---|---|
| **Strategy** | `IPlatformBuildStrategy` + 6 имплементаций | Платформа полностью инкапсулирована, оркестратор платформенно-нейтрален |
| **Template Method** | [BasePlatformBuildStrategy](Assets/NexusBuildPro/Editor/Strategies/BasePlatformBuildStrategy.cs) | `ConfigureBuildOptions`/`Validate*` — общий каркас, наследники переопределяют детали |
| **Pipeline / Chain-of-stages** | `IBuildStep` + `_steps.Sort(Order)` | Упорядоченная цепочка атомарных шагов, каждый возвращает `StepResult` |
| **Facade** | `BuildOrchestrator` | Скрывает профайлер/кэш/историю/шаги за одним API |
| **Observer** | События `OnProgressChanged`, `OnLogEntry`, `OnBuildCompleted`, `OnBuildStarted`, `OnBuildCancelled` | UI полностью декаплирован от domain-а через события |
| **Context Object** | [BuildContext](Assets/NexusBuildPro/Editor/Core/BuildContext.cs) | Единое состояние сборки: профиль, стратегия, прогресс, метаданные, лог |
| **DTO / Value Object** | `BuildResult`, `StepResult`, `OptimizationResult`, `MetricSample` | Иммутабельные снимки, без поведения |
| **Registry (auto-discovery)** | `BuildStrategyRegistry` через `TypeCache.GetTypesDerivedFrom<>` | После фикса — платформы добавляются без правки реестра |
| **Lazy Initialization** | `NexusStyles.???=`, `ChartRenderer.GLMat` | GUIStyle/Material создаются по первому обращению |
| **Cancellation Token** | `CancellationTokenSource` + передача в шаги | Кооперативная отмена через весь пайплайн |
| **Serialization Surrogate** | `BuildManifest : ISerializationCallbackReceiver` | Обход ограничения `JsonUtility` на `Dictionary<>` |

---

## Анализ SOLID

| Принцип | Оценка | Доказательства / замечания |
|---|---|---|
| **SRP** — Single Responsibility | △ | `BuildOrchestrator` (321 стр.) делает слишком много: валидация, запуск пайплайна, подсчёт размера, запись истории, событийная шина. Кандидаты на выделение: `BuildSizeCalculator`, `HistoryRecorder`. |
| **OCP** — Open/Closed | ✓ | Новая платформа = подкласс `BasePlatformBuildStrategy`, регистрация авто (`TypeCache`). Новый шаг = класс реализующий `IBuildStep` + `AddStep()`. |
| **LSP** — Liskov | ✓ | Все стратегии корректно заменяют базу; переопределения не ломают контракт (валидаторы расширяют, а не отменяют). |
| **ISP** — Interface Segregation | △ | `IPlatformBuildStrategy` широкий (11 членов). Можно разнести: `IPlatformConfig` (статические свойства), `IPlatformLifecycle` (`Prepare/Cleanup`), `IPlatformValidator`. Не критично для текущего масштаба. |
| **DIP** — Dependency Inversion | △ | `BuildOrchestrator` зависит от конкретных `BuildCacheManager`, `BuildProfiler`, `BuildHistoryData`. Нет DI-контейнера, нет интерфейсов над ними → юнит-тестировать сложно. Приемлемо для Editor-утилиты, но упомянуть надо. |

## GRASP (по Ларману)

- **Controller** → `BuildOrchestrator`. Центральная точка координации, UI в него только делегирует.
- **Information Expert** → `BuildProfile` знает, как вычислить свои `BuildOptions`, `OutputPath`, `Scenes`. `BuildResult` знает, как отформатировать свой размер/длительность. Поведение живёт рядом с данными.
- **Creator** → `BuildOrchestrator` создаёт `BuildContext` (агрегирует и хранит время жизни). `BuildStrategyRegistry` создаёт стратегии (высокая когезия).
- **Low Coupling** → UI-слой общается с domain-ом **только** через события и публичное API оркестратора. Views не импортируют друг друга.
- **High Cohesion** → Модули разделены по обязанности: `Strategies/` (что где собирать), `Steps/` (пре/пост-обработка), `Profiling/` (метрики), `Cache/` (инкремент), `UI/Views/` (отрисовка).
- **Polymorphism** → Замена `if (target == Android)` на вызов `strategy.PreparePlatform(ctx)`. Это основа OCP здесь.
- **Protected Variations** → `IPlatformBuildStrategy` изолирует ядро от изменений в Unity BuildTarget API; замена API затрагивает только 6 strategy-классов.

---

## Расширяемость

### Добавить новую платформу

```csharp
public sealed class SwitchBuildStrategy : BasePlatformBuildStrategy
{
    public override string PlatformName => "Nintendo Switch";
    public override string PlatformId   => "switch";
    public override BuildTarget      Target      => BuildTarget.Switch;
    public override BuildTargetGroup TargetGroup => BuildTargetGroup.Switch;
    public override string DefaultOutputExtension => ".nsp";
    public override Color  PlatformColor          => new(1f, 0.2f, 0.2f);
}
```

Ничего больше делать не нужно — [BuildStrategyRegistry](Assets/NexusBuildPro/Editor/UI/NexusBuildWindow.cs) подхватит через `TypeCache` при reload домена.

### Добавить шаг пайплайна

```csharp
public sealed class AddressablesPreBuildStep : IBuildStep
{
    public string StepName  => "Build Addressables";
    public int    Order     => 15; // между SceneValidation(10) и AssetOptimization(20)
    public bool   IsEnabled { get; set; } = true;

    public async Task<StepResult> ExecuteAsync(BuildContext ctx, CancellationToken ct)
    { /* ... */ }
}

// Регистрация:
orchestrator.AddStep(new AddressablesPreBuildStep());
```

### Добавить тип чарта

`ChartRenderer` — статический, immediate-mode. Добавить метод типа
`DrawHeatmap(Rect, float[,], ...)` — достаточно одной статической функции, все GL-ресурсы общие.

---

## Безопасность и корректность — что стоит знать

1. **Keystore-пароли** лежат в `ScriptableObject`-ассете (`_keystorePass`, `_keyaliasPass` в [BuildProfile.cs](Assets/NexusBuildPro/Editor/Core/BuildProfile.cs)). Это сериализуется в YAML и попадает в Git. Рекомендация: хранить в `EditorPrefs` / переменных окружения / вынести в отдельный неверсионируемый SO. Оставлено как есть сознательно — иначе ломается UX "один профиль — одна кнопка".
2. **`AssetOptimizationStep` мутирует импортеры на диске** (`SaveAndReimport`). Отката нет. В проде имеет смысл заворачивать в Git-check или делать через `PresetManager`.
3. **Сохранение истории через `EditorPrefs`** — это per-user per-machine. Для CI/team-sharing нужно перейти на `ProjectSettings/*.asset`.
4. **`BuildPipeline.BuildPlayer` — синхронный и блокирующий**. Редактор замирает на время сборки. Это ограничение Unity, не кода.

## Известные ограничения

- **IMGUI, а не UI Toolkit.** Решение сознательное: immediate-mode упрощает реактивность к событиям билда, меньше кода. Ценой — менее гибкий layout и необходимость ручного расчёта `Rect`.
- **Unit-тестов нет.** Структура под них готова (интерфейсы в ядре), но `.asmdef` для `Tests/` не заведён.
- **`AssetOptimizationStep` savings-оценки эвристические** (константы в [AssetOptimizationStep.cs](Assets/NexusBuildPro/Editor/Steps/AssetOptimizationStep.cs)). Для точной цифры нужно считать pre/post размер через `BuildReport.GetFiles()`.
- **Incremental-кэш сейчас не подаёт `IsAssetCached` в шаги** — манифест строится и коммитится, но ни один `IBuildStep` его не читает. Это hook-point для будущей оптимизации (skip re-import неизменённых текстур).

---

## Недавние исправления (этот коммит)

В ходе код-ревью выявлено и исправлено:

| # | Файл | Проблема | Фикс |
|---|---|---|---|
| 1 | [BuildCacheManager.cs](Assets/NexusBuildPro/Editor/Cache/BuildCacheManager.cs) | `SerializableDictionary : Dictionary<>` **не сериализуется** `JsonUtility` (документированное ограничение Unity). Манифест всегда пустой → инкрементальный кэш не работал. | Заменено на `ISerializationCallbackReceiver` с round-trip через `List<ManifestEntry>`. |
| 2 | [BuildCacheManager.cs](Assets/NexusBuildPro/Editor/Cache/BuildCacheManager.cs) | `Application.dataPath.Replace("/Assets", "")` ломается на путях типа `C:/Assets/Proj/Assets` (заменяет все вхождения). | Переход на `Path.GetDirectoryName(Application.dataPath)`. |
| 3 | [BuildOrchestrator.cs](Assets/NexusBuildPro/Editor/Core/BuildOrchestrator.cs) | `_cache` создавался, но **никогда не использовался** — `BeginSession/CommitSession` не вызывались. | Добавлены в `BuildAsync` до пайплайна и после успеха. |
| 4 | [NexusStyles.cs](Assets/NexusBuildPro/Editor/UI/NexusStyles.cs) | `MakeTex` создавал новый `Texture2D` при каждом вызове — ~40+ текстур за один `ResetStyles()`. GPU leak. | Введён color-keyed кэш `Dictionary<Color, Texture2D>`. |
| 5 | [ChartRenderer.cs](Assets/NexusBuildPro/Editor/UI/ChartRenderer.cs) | `_glMaterial` не уничтожался при reload домена → "Cleaning up leaked objects" warning. | Подписка на `AssemblyReloadEvents.beforeAssemblyReload` + `EditorApplication.quitting`, `DestroyImmediate`. |
| 6 | [NexusBuildWindow.cs](Assets/NexusBuildPro/Editor/UI/NexusBuildWindow.cs) | `BuildStrategyRegistry` хардкодил 6 платформ — нарушение OCP. | Переход на `TypeCache.GetTypesDerivedFrom<BasePlatformBuildStrategy>()`. |
| 7 | [NexusBuildWindow.cs](Assets/NexusBuildPro/Editor/UI/NexusBuildWindow.cs) | Мёртвые поля `_pendingBuildTrigger/_pendingProfile/_pendingStrategy` — заявленная в комментарии логика не реализована. | Удалены. |
| 8 | [BuildContext.cs](Assets/NexusBuildPro/Editor/Core/BuildContext.cs) | `Log => _log` возвращал `List<>` под видом `IReadOnlyList<>` — можно откастовать и мутировать. | Возврат `_log.AsReadOnly()`. |
| 9 | [BuildProfiler.cs](Assets/NexusBuildPro/Editor/Profiling/BuildProfiler.cs) | `GetSlowestBuildTime()` возвращал `0` и при пустом списке, и при максимуме = 0 — неразличимо. | Guard + `float.MinValue` sentinel. |
| 10 | [AssetOptimizationStep.cs](Assets/NexusBuildPro/Editor/Steps/AssetOptimizationStep.cs) | Magic numbers `1024*1024`, `0.7f`, `0.65f`, `2048` — прямо в теле цикла. | Вынесены в именованные `const`-ы с комментарием, что это эвристика. |
| 11 | [DashboardView.cs](Assets/NexusBuildPro/Editor/UI/Views/DashboardView.cs) | Расчёт `cardSpacing` от ширины давал отрицательные значения на узких окнах → overlap карточек. | Фиксированный spacing + `Mathf.Max` на ширину карты. |
| 12 | Минор | Неиспользуемые поля `_scroll`, `_assetScanDone` в нескольких Views. | Удалены. |

---

## Структура проекта

```
Assets/NexusBuildPro/Editor/
├── NexusBuildPro.Editor.asmdef            — Editor-only assembly
├── Core/
│   ├── BuildContext.cs                    — состояние одной сборки
│   ├── BuildOrchestrator.cs               — центральный координатор (Facade)
│   ├── BuildProfile.cs                    — ScriptableObject-конфигурация
│   ├── BuildResult.cs                     — immutable результат
│   └── Interfaces/
│       ├── IBuildStep.cs                  — шаг пайплайна
│       ├── IBuildOptimizer.cs             — оптимизатор (reserved)
│       └── IPlatformBuildStrategy.cs      — контракт платформы
├── Strategies/
│   ├── BasePlatformBuildStrategy.cs       — Template Method база
│   ├── WindowsBuildStrategy.cs
│   ├── LinuxBuildStrategy.cs
│   ├── MacOSBuildStrategy.cs
│   ├── AndroidBuildStrategy.cs            — keystore, APK/AAB
│   ├── iOSBuildStrategy.cs
│   └── WebGLBuildStrategy.cs              — compression, COOP/COEP warning
├── Steps/
│   ├── SceneValidationStep.cs             — order 10
│   ├── AssetOptimizationStep.cs           — order 20
│   └── PostBuildStep.cs                   — order 200
├── Cache/
│   └── BuildCacheManager.cs               — MD5-manifest, инкрементальный кэш
├── Data/
│   └── BuildHistoryData.cs                — EditorPrefs-персист истории
├── Profiling/
│   ├── BuildMetrics.cs                    — таймеры шагов, Stopwatch-ы
│   └── BuildProfiler.cs                   — сессии, агрегаты, trend
└── UI/
    ├── NexusBuildWindow.cs                — главное EditorWindow + Registry
    ├── NexusStyles.cs                     — палитра, GUIStyle-кэш
    ├── ChartRenderer.cs                   — GL-примитивы: line/bar/pie/gantt/ring
    └── Views/
        ├── DashboardView.cs               — метрики + trend
        ├── PlatformConfigView.cs          — сетка платформ
        ├── OptimizationView.cs            — кэш + рекомендации
        ├── ProfilerView.cs                — Gantt + live ring + breakdown
        └── BuildHistoryView.cs            — таблица + bar + pie
```

---

## Точки входа для ревью

Если читать код впервые — рекомендованный порядок:

1. [BuildProfile.cs](Assets/NexusBuildPro/Editor/Core/BuildProfile.cs) — понять, что такое "конфигурация сборки".
2. [IPlatformBuildStrategy.cs](Assets/NexusBuildPro/Editor/Core/Interfaces/IPlatformBuildStrategy.cs) → [BasePlatformBuildStrategy.cs](Assets/NexusBuildPro/Editor/Strategies/BasePlatformBuildStrategy.cs) → [AndroidBuildStrategy.cs](Assets/NexusBuildPro/Editor/Strategies/AndroidBuildStrategy.cs) — Strategy + Template Method в действии.
3. [IBuildStep.cs](Assets/NexusBuildPro/Editor/Core/Interfaces/IBuildStep.cs) → [SceneValidationStep.cs](Assets/NexusBuildPro/Editor/Steps/SceneValidationStep.cs) — пайплайн.
4. [BuildOrchestrator.cs](Assets/NexusBuildPro/Editor/Core/BuildOrchestrator.cs) — как всё собирается вместе. Обратить внимание на комментарии про `GetAwaiter().GetResult()`.
5. [NexusBuildWindow.cs](Assets/NexusBuildPro/Editor/UI/NexusBuildWindow.cs) — UI-композиция и `delayCall`-трюк для обхода Unity 6 player-loop check.
