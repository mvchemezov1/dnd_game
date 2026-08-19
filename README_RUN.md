# Запуск DnD Game — пошаговая инструкция

Этот файл описывает, как запустить проект **как есть сейчас** (реализован модуль персонажей — CRUD через REST API + HTML-клиент). Ниже — два способа: локально (.NET SDK + своя PostgreSQL) и через Docker (`docker compose`).

Вместе с этим README прилагаются исправленные/добавленные файлы, без которых Docker-запуск не работал бы (см. раздел «Что было исправлено и почему» в конце).

---

## Способ 1. Локальный запуск (.NET SDK + PostgreSQL)

### Предварительные требования
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) — проверить: `dotnet --version`
- PostgreSQL 16 (локально или в контейнере), с созданной базой `dnd_game`

### Шаги

1. **Поднять PostgreSQL**, если её ещё нет. Проще всего — временным контейнером:
   ```bash
   docker run -d --name dnd_pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=dnd_game -p 5432:5432 postgres:16
   ```
   (таблица `events` создастся автоматически при первом запуске приложения — миграции не нужны)

2. **Указать строку подключения.** Используйте приложенный `appsettings.Development.json` (положите его рядом с `appsettings.json` в корне проекта) — он уже указывает на `localhost:5432`, БД `dnd_game`, пользователь `postgres`, пароль `postgres`. Если у вас другие учётные данные — поправьте `Password=` в этом файле.

3. **Запустить приложение:**
   ```bash
   dotnet restore
   dotnet run
   ```
   При обычном `dotnet run` ASP.NET Core сам подставит `appsettings.Development.json` поверх `appsettings.json` (переменная `ASPNETCORE_ENVIRONMENT=Development` уже прописана в `Properties/launchSettings.json`).

4. **Открыть в браузере:** http://localhost:5000 — увидите HTML-страницу со списком персонажей и формой создания.

5. **Или обратиться напрямую к API:**
   ```bash
   curl -X POST http://localhost:5000/api/characters \
     -H "Content-Type: application/json" \
     -d '{"characterId":"11111111-1111-1111-1111-111111111111","name":"Aria","maxHitPoints":24}'

   curl http://localhost:5000/api/characters
   ```

---

## Способ 2. Docker (docker compose)

### Предварительные требования
- Docker Engine + Docker Compose v2 (`docker compose version`)

### Файлы, которые нужно положить в корень проекта
Скопируйте туда (с заменой) файлы из этой поставки:
- `Dockerfile` — заменяет исходный (был нерабочим, см. ниже)
- `docker-compose.yml` — заменяет исходный
- `.dockerignore` — новый файл
- `.env` — новый файл (реальные значения для docker-compose; `.env.example` в репозитории остаётся как образец)
- `Program.cs` — заменяет исходный (два точечных исправления, см. ниже)

### Шаги

1. Из корня проекта:
   ```bash
   docker compose up --build
   ```
   Первый запуск соберёт образ (компиляция через `dotnet publish` внутри контейнера) и поднимет PostgreSQL с healthcheck — приложение стартует только после того, как БД реально готова принимать соединения.

2. Открыть http://localhost:5000

3. Остановить: `Ctrl+C`, затем `docker compose down` (данные PostgreSQL сохранятся в volume `dnd_game_pgdata`; для полной очистки — `docker compose down -v`).

---

## Что было исправлено и почему

Оригинальные `Dockerfile`, `docker-compose.yml` и `Program.cs` в репозитории не позволяли запустить приложение через Docker. Правки точечные, поведение при обычном `dotnet run` не меняется.

| Файл | Проблема | Исправление |
|---|---|---|
| `Dockerfile` | Однослойная сборка на образе `aspnet:8.0` (без SDK) делала `COPY . .` + `dotnet dnd_game.dll`, но `.dll` никогда не собирался — образ падал при старте | Multi-stage: сборка на `sdk:8.0` (`dotnet publish`), рантайм — на лёгком `aspnet:8.0` |
| `Program.cs` | Kestrel слушал `http://localhost:5000` — это только loopback-интерфейс **внутри контейнера**; проброшенный порт Docker до него не достучится | Слушает `http://0.0.0.0:5000` (адрес настраивается через `APP_URL`) |
| `Program.cs` | Повторный `AddJsonFile("appsettings.json")` добавлялся в конфигурацию ПОСЛЕ переменных окружения и молча перезаписывал их — переменная `ConnectionStrings__DefaultConnection` из `docker-compose.yml` игнорировалась | Строка удалена; конфигурация читается в штатном порядке ASP.NET Core (env vars переопределяют файл) |
| `docker-compose.yml` | Строка подключения в `appsettings.json` — `Host=localhost`; внутри контейнера `app` это сам контейнер, а не БД | Добавлена переменная `ConnectionStrings__DefaultConnection` с `Host=db` (имя сервиса) |
| `docker-compose.yml` | `app` мог стартовать раньше, чем PostgreSQL готова принимать соединения (`PostgresEventStore` подключается синхронно при старте и падает, если БД не отвечает) | Добавлен `healthcheck` (`pg_isready`) + `depends_on: condition: service_healthy` |
| `docker-compose.yml` | Порты `"5000:80"` не совпадали с портом, который реально слушало приложение (5000) | Исправлено на `"5000:5000"` |
| — | Данные PostgreSQL нигде не сохранялись (при пересоздании контейнера терялись) | Добавлен именованный volume `dnd_game_pgdata` |
| — | Отсутствовал `.dockerignore` — в образ копировались `bin/`, `obj/`, `.vs/`, тесты, доки | Добавлен `.dockerignore` |

## Ограничения проверки

Правки проверены построчным разбором кода и логики ASP.NET Core / Docker (по официальной документации поведения `WebApplication.CreateBuilder`, `AddJsonFile`, Kestrel binding и `docker compose healthcheck`). Фактическая сборка `dotnet publish` и `docker compose up` **не выполнялись** — в среде, где готовился этот документ, нет .NET SDK и нет доступа к Docker Hub / mcr.microsoft.com. Перед вводом в постоянное использование рекомендуется прогнать `docker compose up --build` у себя и свериться с логами.
