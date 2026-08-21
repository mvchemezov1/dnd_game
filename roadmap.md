%%{init: {'theme': 'base', 'themeVariables': { 'primaryColor': '#ffcc00', 'primaryTextColor': '#000', 'primaryBorderColor': '#333', 'lineColor': '#888', 'secondaryColor': '#e0e0e0'}}}%%
graph TD
    subgraph Application["Прикладной слой (Application)"]
        A1[Обработчики команд/событий]
        A2[Проекции чтения]
        A3[Сервисы (Crafting, Trade, Travel)]
        A4[Агрегаты (Character, Combat, Campaign)]
        A5[Саги (Quest, Combat, LevelUp)]
        A6[Пространства имён]
    end

    subgraph Infrastructure["Инфраструктура"]
        I1[Event Store & Snapshot]
        I2[Message Bus (InMemory, RabbitMQ)]
        I3[Безопасность (Auth, Tokens)]
        I4[Сеть (GameServer, SessionManager)]
        I5[AI (MonsterAI, Perception, Scripts)]
        I6[Мир (Grid, Visibility, Objects)]
        I7[Координация (DistributedLock, SagaCoordinator)]
        I8[Мониторинг (Health, Metrics, Tracing)]
    end

    subgraph Presentation["Презентационный слой"]
        P1[REST API (Controllers)]
        P2[WebSocket (хаб, клиент)]
        P3[Интерфейс ввода (InputHandler, Macros)]
        P4[DM-инструменты (UI, OverrideCommands)]
        P5[Регистрация зависимостей]
    end

    subgraph Migrations["Миграции БД"]
        M1[Дублирование индексов/столбцов]
        M2[Отсутствие внешних ключей]
        M3[Синхронный мигратор, жёсткий путь]
        M4[Нет таблицы saga_state]
    end

    subgraph Frontend["Клиентская часть"]
        F1[Auth & UI-helpers]
        F2[Несоответствие API (DTO, поля)]
        F3[WebSocket клиент (отсутствует)]
        F4[Макросы и обработка команд]
        F5[CSS/стили]
    end

    subgraph SharedKernel["Общие компоненты"]
        S1[Дублирование констант / перечислений]
        S2[Дублирование исключений]
        S3[Дублирование базовых интерфейсов и утилит]
        S4[Мёртвый код (Result, Maybe, IRepository)]
    end

    %% Связи между слоями и проблемами
    A2 -->|"Не инициализация полей, RebuildAsync неполный"| I1
    A3 -->|"Не передача количества, семантические ошибки"| P1
    A4 -->|"Необработанные события, некорректные проверки"| A1
    A5 -->|"Неверные награды, фиксированные hitDieType"| A4

    I1 -->|"Дублирование событий при снапшотах, игнор токенов"| I2
    I2 -->|"SendAsync не работает, Subscribe пуст, глотает ошибки"| I7
    I3 -->|"Не заполняет OwnedCharacterIds, нет rate limiting"| I4
    I4 -->|"Аутентификация заглушка, проблемы с удалением"| P2
    I5 -->|"Двойные проверки, заглушки восприятия"| I6
    I6 -->|"Дублирование IGridProvider, конфликт типов"| S1
    I7 -->|"Блокировка по SagaId, не по CorrelationId"| I2
    I8 -->|"HealthCheck не проверяет реальное соединение"| I2

    P1 -->|"Ошибка атрибутов, дублирование DTO"| S1
    P1 -->|"Использование доменной команды без DTO"| A1
    P2 -->|"SubscribeToEvents повторно подписывает, нет фильтрации"| I2
    P3 -->|"HandleExamine не отправляет команду, парсинг без проверки"| A1
    P4 -->|"ChangeFactionReputation с неверным параметром"| A4
    P5 -->|"Дублирование регистраций, конфликт с AddControllers"| P1

    M1 -->|"Дублирование"| S1
    M2 -->|"Нарушение ссылочной целостности"| I1
    M3 -->|"Синхронный вызов"| I8
    M4 -->|"Отсутствие хранения состояния саг"| A5

    F1 -->|"authFetch не сохраняет URL, двойная тема"| I3
    F2 -->|"Несовместимые DTO (CharacterId, поля)"| P1
    F3 -->|"Отсутствует websocket-client.js"| P2
    F4 -->|"Подстановка переменных не работает, макросы с ошибками"| P3
    F5 -->|"Отсутствуют стили для UI-компонентов"| P3

    S1 -->|"Конфликты типов, лишний код"| S2
    S2 -->|"Не обрабатываются глобально"| P1
    S3 -->|"Несовместимость с доменными интерфейсами"| A1
    S4 -->|"Мёртвый код"| S1

    %% Критические риски
    A1 & A2 & A3 & A4 & A5 -->|"Отсутствие проверок/токенов"| Risk1(Отказоустойчивость)
    I1 & I2 & I3 & I4 & I5 & I6 & I7 & I8 -->|"Синхронные блокировки, утечки"| Risk2(Производительность)
    P1 & P2 & P3 & P4 & F2 & F3 -->|"Несоответствие API, потеря данных"| Risk3(Надёжность)
    S1 & S2 & S3 & M1 & F1 -->|"Дублирование и несогласованность"| Risk4(Поддерживаемость)
