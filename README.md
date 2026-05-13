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
