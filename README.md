# Channel Routing - Решение задачи трассировки канала

Реализация классической задачи детальной трассировки канала в САПР интегральных схем с использованием левостороннего алгоритма (Left-Edge Algorithm).

## 📋 Описание задачи

Канал представляет собой линейку колонок с контактами сверху и снизу. Каждый контакт помечен номером цепи (0 = пусто). Цель - проложить соединения между контактами одной цепи, используя минимальное количество горизонтальных треков.

### Формат входных данных
```
<число колонок N>
<верхний ряд: цепь1 цепь2 ... цепьN>
<нижний ряд: цепь1 цепь2 ... цепьN>
```

## 🏗️ Архитектура проекта

Проект организован по принципам **Clean Architecture** и разделён на слои:

### 1. Domain Layer (Доменный слой)
**Папка**: `Domain/Entities/`

Содержит бизнес-сущности и основную логику предметной области:
- **Channel** - представление канала с контактами
- **Net** - цепь, соединяющая контакты
- **Contact** - контактная точка (верхняя/нижняя)
- **Segment** - сегмент трассировки (горизонтальный/вертикальный)
- **RoutingResult** - результат работы алгоритма с метриками

**Принципы**:
- Инкапсуляция бизнес-правил
- Валидация данных в конструкторах
- Immutable свойства где возможно

### 2. Application Layer (Слой приложения)
**Папки**: `Application/Interfaces/`, `Application/Algorithms/`, `Application/Services/`

Содержит бизнес-логику и алгоритмы:
- **IRoutingAlgorithm** - интерфейс для алгоритмов (Strategy pattern)
- **RoutingAlgorithmBase** - базовый класс с общей функциональностью
- **LeftEdgeAlgorithm** - реализация левостороннего алгоритма
- **ChannelDataGenerator** - генератор тестовых данных (Factory pattern)
- **ChannelBuilder** - построитель каналов (Builder pattern)

### 3. Infrastructure Layer (Инфраструктурный слой)
**Папки**: `Infrastructure/IO/`, `Infrastructure/Visualization/`

Содержит технические сервисы:
- **ChannelFileReader** - чтение данных из файлов
- **ChannelFileWriter** - запись результатов в файлы
- **ConsoleVisualizer** - визуализация в консоли
- **SvgVisualizer** - генерация SVG-диаграмм

### 4. Presentation Layer (Слой представления)
**Папка**: `Presentation/`

Содержит пользовательский интерфейс:
- **Program** - консольное приложение с меню

## 🎨 Использованные паттерны проектирования

### 1. Strategy (Стратегия)
**Где**: `IRoutingAlgorithm`, `LeftEdgeAlgorithm`

Позволяет легко добавлять новые алгоритмы трассировки:
```csharp
IRoutingAlgorithm algorithm = new LeftEdgeAlgorithm();
RoutingResult result = algorithm.Route(channel);
```

### 2. Factory (Фабрика)
**Где**: `ChannelDataGenerator`

Инкапсулирует создание сложных объектов:
```csharp
var generator = new ChannelDataGenerator();
var channel = generator.GenerateSimpleChannel(width: 10, netCount: 5);
```

### 3. Builder (Строитель)
**Где**: `ChannelBuilder`

Пошаговое конструирование сложных каналов:
```csharp
var channel = new ChannelBuilder(10)
    .AddNet(1, topColumn: 0, bottomColumn: 5)
    .AddNet(2, topColumn: 2, bottomColumn: 7)
    .Build();
```

### 4. Template Method (Шаблонный метод)
**Где**: `RoutingAlgorithmBase`

Базовый класс определяет общий алгоритм работы:
```csharp
public RoutingResult Route(Channel channel)
{
    // Общая логика: замер времени, обработка ошибок
    ExecuteRouting(channel, segments, conflicts); // Переопределяется в наследниках
    // Создание результата
}
```

### 5. Dependency Inversion Principle (DIP)
Высокоуровневые модули зависят от абстракций, а не от конкретных реализаций:
- `IRoutingAlgorithm` - интерфейс вместо конкретного класса
- Лёгкая замена алгоритмов и компонентов

## 🔍 Принципы SOLID

### Single Responsibility Principle (SRP)
Каждый класс имеет одну ответственность:
- `Channel` - только представление данных канала
- `LeftEdgeAlgorithm` - только алгоритм трассировки
- `ConsoleVisualizer` - только визуализация в консоли

### Open/Closed Principle (OCP)
Классы открыты для расширения, закрыты для модификации:
- Новые алгоритмы добавляются через `IRoutingAlgorithm`
- Новые генераторы расширяют `ChannelDataGenerator`

### Liskov Substitution Principle (LSP)
Любой алгоритм, реализующий `IRoutingAlgorithm`, взаимозаменяем.

### Interface Segregation Principle (ISP)
Интерфейсы специфичны и минимальны.

### Dependency Inversion Principle (DIP)
Зависимость от абстракций, а не от конкретики.

## 🧮 Левосторонний алгоритм (Left-Edge Algorithm)

### Принцип работы:

1. **Сортировка цепей** по левому краю (leftmost column)
2. **Назначение на треки**:
   - Для каждой цепи находим первый доступный трек
   - Трек доступен, если нет пересечений с уже размещёнными цепями
   - Если нет доступных треков - создаём новый
3. **Создание сегментов**:
   - Горизонтальный сегмент от левого до правого края цепи
   - Вертикальные сегменты для соединения с контактами

### Сложность:
- Временная: **O(n²)** где n - число цепей
- Пространственная: **O(n + m)** где m - число треков

### Преимущества:
- Простота реализации
- Гарантированное решение для разводимых каналов
- Быстрая работа на практических задачах

## 🚀 Запуск и использование

### Компиляция:
```bash
cd /home/claude/ChannelRouting
dotnet build
```

### Запуск:
```bash
dotnet run
```

### Основные функции:

1. **Простой пример** - демонстрация на малом канале
2. **Пользовательский канал** - создание канала заданного размера
3. **Загрузка из файла** - чтение данных из текстового файла
4. **Benchmark** - тестирование производительности
5. **Канал с конфликтами** - генерация сложных случаев
6. **Генерация тестовых данных** - создание файлов для тестирования

## 📊 Метрики

Для каждого результата трассировки вычисляются:
- **Число треков** - количество использованных горизонтальных слоёв
- **Длина проводов** - суммарная длина всех сегментов
- **Конфликты** - наличие пересечений цепей на одном треке
- **Время выполнения** - производительность алгоритма

## 📁 Структура файлов

```
ChannelRouting/
├── Domain/
│   └── Entities/
│       ├── Channel.cs              # Канал с контактами
│       ├── Net.cs                  # Цепь
│       ├── SegmentAndContact.cs    # Сегменты и контакты
│       └── RoutingResult.cs        # Результат трассировки
├── Application/
│   ├── Interfaces/
│   │   └── IRoutingAlgorithm.cs    # Интерфейс алгоритмов
│   ├── Algorithms/
│   │   └── LeftEdgeAlgorithm.cs    # Левосторонний алгоритм
│   └── Services/
│       └── ChannelDataGenerator.cs # Генератор данных
├── Infrastructure/
│   ├── IO/
│   │   └── FileServices.cs         # Работа с файлами
│   └── Visualization/
│       ├── ConsoleVisualizer.cs    # Консольный вывод
│       └── SvgVisualizer.cs        # SVG-диаграммы
├── Presentation/
│   └── Program.cs                  # Главное приложение
└── ChannelRouting.csproj           # Файл проекта
```

## 🎯 Примеры использования в коде

### Создание и трассировка канала:
```csharp
// Генерация канала
var generator = new ChannelDataGenerator();
var channel = generator.GenerateSimpleChannel(width: 10, netCount: 5);

// Применение алгоритма
var algorithm = new LeftEdgeAlgorithm();
var result = algorithm.Route(channel);

// Визуализация
var visualizer = new ConsoleVisualizer();
visualizer.DisplayRoutingResult(result);

// Сохранение в SVG
var svgVisualizer = new SvgVisualizer();
svgVisualizer.SaveToFile(result, "output.svg");
```

### Загрузка из файла:
```csharp
var reader = new ChannelFileReader();
var channel = reader.ReadFromFile("channel.txt");
```

### Пользовательский канал:
```csharp
var channel = new ChannelBuilder(8)
    .AddNet(1, topColumn: 0, bottomColumn: 5)
    .AddNet(2, topColumn: 1, bottomColumn: 7)
    .AddNet(3, topColumn: 4, bottomColumn: 2)
    .Build();
```

## 📈 Результаты

Программа выводит:
- Визуализацию канала в консоли с цветовой кодировкой
- Метрики производительности
- SVG-диаграммы для графического представления
- Детальные отчёты в текстовых файлах

## 🔧 Расширение функциональности

Для добавления нового алгоритма:

1. Создайте класс, наследующий `RoutingAlgorithmBase`
2. Реализуйте метод `ExecuteRouting`
3. Используйте через интерфейс `IRoutingAlgorithm`

Пример:
```csharp
public class YoshimuraAlgorithm : RoutingAlgorithmBase
{
    public override string Name => "Yoshimura Algorithm";
    
    protected override int ExecuteRouting(
        Channel channel, 
        List<Segment> segments, 
        List<string> conflicts)
    {
        // Реализация эвристического алгоритма
        // с учётом вертикальных ограничений
    }
}
```

## 📚 Теоретическая база

**Задача трассировки канала** - классическая NP-полная задача в САПР СБИС.

**Левосторонний алгоритм** - жадный эвристический алгоритм, оптимальный для многих практических случаев.

**Алгоритм Йошимуры-Куха** описан в техническом отчёте Berkeley [`Efficient Algorithms for Channel Routing`](https://www2.eecs.berkeley.edu/Pubs/TechRpts/1980/29075.html) (T. Yoshimura, E. S. Kuh, UCB/ERL M80/43, 1980) и последующей журнальной версии IEEE TCAD 1982. В оригинальной работе предложены два алгоритма, которые не просто назначают отдельные цепи на треки, а **сливают совместимые цепи** (net merging), чтобы уменьшить высоту канала через минимизацию длины критического пути в VCG.

### Соответствие реализации алгоритму Йошимуры-Куха

> Текущий класс `YoshimuraAlgorithm` постепенно приближен к классическому Yoshimura-Kuh: теперь он использует вынесенные классы VCG, HCG, HNCG, event/sweep-line zone table, генерирует merge-кандидатов из границ соседних зон, выбирает независимый batch merge-кандидатов точным weighted bipartite matching с учётом longest path и назначает треки уже composite net'ам. Это всё ещё не полная промышленная реализация: Algorithm #2 теперь использует точный weighted bipartite matching на локальных границах зон, а dogleg/split-routing реализован как relaxation циклических VCG-рёбер без полной физической jog/via геометрии.

| Раздел/идея из статьи Yoshimura-Kuh | Где сейчас в коде | Степень соответствия | Что нужно добавить для приближения к Algorithm #1/#2 |
| --- | --- | --- | --- |
| Постановка двухслойной channel routing задачи: горизонтальные сегменты на одном слое, вертикальные соединения на другом, vias между слоями | `Channel`, `Net`, `Segment`, `CreateVerticalSegments(...)` в `YoshimuraAlgorithm` | **Частично соответствует**: модель контактов, горизонтальных и вертикальных сегментов есть, но слой/переход via не моделируются как отдельные сущности | Явно хранить слой сегмента, via-точки и ограничения технологического стека, если нужно сравнивать с физической моделью статьи |
| Vertical Constraint Graph (VCG): если в колонке верхний net должен быть выше нижнего, появляется ориентированное ограничение | `VerticalConstraintGraph`: build, topological order, reachability, longest path, update-after-merge | **В основном соответствует** | Оптимизировать reachability/longest path кэширование для больших графов |
| Проверка ацикличности VCG и обработка невозможных вертикальных ограничений | `BreakCyclesWithDoglegs(...)` ослабляет циклические VCG-рёбра и продолжает routing по repaired graph | **Частично соответствует**: циклы конструктивно ремонтируются на уровне ограничений, но без явной физической jog/via геометрии | Добавить полноценные dogleg/subnet-сущности и восстановление jog-сегментов в результате |
| Горизонтальные ограничения/HCG: два net не могут лежать на одном треке, если их интервалы пересекаются | `HorizontalConstraintGraph` плюс финальная проверка `trackIntervals` | **В основном соответствует** | Перевести active-set построение HCG на более быстрые структуры для очень плотных каналов |
| Zone representation: разбиение канала на зоны, где активные интервалы net образуют локальные множества/клики HCG | `ZoneTable` строится event/sweep-line диапазонами зон и генерирует пары merge-кандидатов на соседних границах | **В основном соответствует Algorithm #1 на уровне candidate generation** | Добавить инкрементальное обновление отдельных затронутых зон вместо пересборки таблицы после batch merge |
| Net merging: совместимые net без горизонтального конфликта и без пути зависимости в VCG могут быть слиты и затем занимать один трек | `CompositeNet`, `HorizontalNonConstraintGraph`, `MergePlanner`, `SelectZoneLocalMergeBatch(...)` | **В основном соответствует базовой идее Yoshimura-Kuh** | Добавить точные benchmark-сценарии из публикаций и сравнить последовательности merge |
| Longest-path objective: минимизировать длину максимального пути в VCG, так как она задаёт нижнюю оценку/требуемую высоту при вертикальных ограничениях | `LongestPathAfterMerge(...)` входит в вес рёбер `WeightedBipartiteMatcher` | **В основном соответствует для локального matching** | Добавить кэширование longest-path оценок и paper benchmark-сравнение качества |
| Algorithm #1 Yoshimura-Kuh: последовательное сканирование зон и выбор merge-пар эвристикой, связанной с VCG/longest path | `YoshimuraZoneScanner` сканирует `ZoneTable.GetBoundaryCandidateSets(...)` | **В основном реализовано как отдельный zone-scan сервис** | Добавить локальное обновление зон после каждого merge внутри scan вместо batch-level пересборки |
| Algorithm #2 Yoshimura-Kuh: улучшенный выбор merge через matching/bipartite matching для лучшего уменьшения longest path | `YoshimuraZoneScanner` + `WeightedBipartiteMatcher` выбирают точное maximum-weight matching на локальных candidate graph'ах | **В основном реализовано для локальных зон** | Добавить benchmark-сравнение с примерами статьи и при необходимости заменить min-cost flow на специализированный Hungarian/bitset matcher |
| Финальное назначение треков после merging | `RouteCompositeNets(...)`: назначение треков composite net'ам и разворачивание исходных `Net` в сегменты | **В основном соответствует базовой фазе после merge** | Улучшить разворачивание composite net с явными vias/stubs и dogleg-сегментами при расширении модели |
| Dogleg-mode / split-routing | `BreakCyclesWithDoglegs(...)` relaxes VCG cycle edges перед merge/routing | **Частично реализовано**: repaired graph строится, но физические dogleg/jog segments пока не представлены отдельными сущностями | Добавить subnets, правила минимальной длины jog и восстановление маршрута с вертикальными dogleg-соединениями |

### Рекомендуемый план доработки Yoshimura/Kuh до Algorithm #1/#2

1. **Оптимизировать выделенные графы**: `VerticalConstraintGraph`, `HorizontalConstraintGraph`, `HorizontalNonConstraintGraph` уже вынесены в отдельные классы; следующий шаг — добавить bitset/reachability-кэширование для больших задач.
2. **Оптимизировать zone representation**: `ZoneTable` уже использует event/sweep-line диапазоны; следующий шаг — обновлять только затронутые зоны после merge, а не пересобирать таблицу целиком.
3. **Расширить проверку merge-совместимости**: HNCG уже добавлен; следующий шаг — хранить adjacency sets совместимости и обновлять только соседей merged composite net.
4. **Доработать incremental update**: VCG/HCG/HNCG/ZoneTable синхронизируются после merge-pass; следующий шаг — заменить batch-level пересборку на локальные updates затронутых рёбер/зон.
5. **Улучшить longest-path scoring**: `LongestPathAfterMerge` уже влияет на exact local matching; следующий шаг — кэшировать оценки и считать exact longest path только для top-K кандидатов.
6. **Algorithm #1**: текущий проход уже вынесен в `YoshimuraZoneScanner`; следующий шаг — локально обновлять графы после каждого merge внутри zone scan.
7. **Algorithm #2**: exact weighted bipartite matching уже добавлен для локальных зон; следующий шаг — benchmark/оптимизация matcher'а на больших candidate graph'ах.
8. **Dogleg-mode**: VCG cycle relaxation уже добавлен; следующий шаг — делить net на subnets и создавать явные jog/dogleg segments в результате.
9. **Тесты на соответствие статье**: добавить benchmark-каналы из публикаций/лекций, тесты на построение зон, допустимость merge, изменение longest path и корректное разворачивание composite net в сегменты.

**Применение**: автоматизированное проектирование интегральных схем, печатных плат, межсоединений в FPGA.

## 👨‍💻 Автор

Реализация выполнена с соблюдением:
- Принципов Clean Code
- SOLID principles
- Design Patterns (Strategy, Factory, Builder, Template Method)
- Clean Architecture
- Лучших практик C# и .NET

## 📝 Лицензия

Учебный проект для изучения алгоритмов САПР и паттернов проектирования.