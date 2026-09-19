# Tarot Reading Service

Backend-сервис раскладов Таро на ASP.NET Core 8 + PostgreSQL.

## О проекте

Предметная область — сервис гаданий на картах Таро:
- **колоды** и **карты**;
- **шаблоны раскладов** (spreads);
- **пользовательские гадания** (readings) — сессии с выбранными картами;
- JWT-аутентификация, Redis-кэш, Liquibase-миграции.

## Запуск

```bash
docker compose up --build
```

После старта:
- API / Swagger: http://localhost:8080
- Health: http://localhost:8080/health
- PostgreSQL **Primary**: `localhost:5437` (`db_primary`)
- PostgreSQL **Replica**: `localhost:5438` (`db_replica`)
- Redis: `localhost:6379`

Миграции Liquibase применяются автоматически сервисом `liquibase` до старта `webapp`.

## Архитектура

```
Client
  ↓
ASP.NET Core Controllers
  ↓
Services
  ↓
Repositories (EF Core / Dapper) — запись на Primary, чтение readings с Replica
  ↓
PostgreSQL Primary  ──streaming WAL──►  PostgreSQL Replica
```

Дополнительно: Redis (кэш карт), JWT Auth, FluentValidation, background jobs партиционирования, Read Scaling (Lab 4).

## Схема БД

| Таблица | Описание |
|---------|----------|
| `decks` | Колоды Таро |
| `cards` | Карты (FK → decks) |
| `spreads` | Шаблоны раскладов |
| `spread_cards` | M:N spreads ↔ cards |
| `users` | Пользователи |
| `api_keys` | API-ключи |
| `readings` | Пользовательские гадания (**scaling entity**, RANGE partition по `created_at`) |
| `reading_cards` | Карты конкретного гадания |

Связи: PK/FK, one-to-many (`decks→cards`, `users→readings`), many-to-many (`spreads↔cards` через `spread_cards`, `readings↔cards` через `reading_cards`).

## Основная сущность для масштабирования

**Таблица:** `readings`

**Почему подходит:** каждое обращение пользователя к сервису создаёт новое гадание. Объём растёт пропорционально аудитории и частоте запросов (в отличие от относительно статичных `cards`/`decks`/`spreads`). Есть `created_at` для партиционирования и фильтров по периоду.

## Основные endpoint'ы

| Method | Path | Описание |
|--------|------|----------|
| GET | `/health` | Health check (PostgreSQL + Redis) |
| GET/POST | `/api/auth/*` | Регистрация / логин |
| CRUD | `/api/card` | Карты (+ pagination, search, suit, sort) |
| GET | `/api/card/deck/{deckId}` | Карты колоды |
| CRUD | `/api/deck` | Колоды |
| CRUD | `/api/spread` | Шаблоны раскладов |
| POST/DELETE | `/api/spread/{id}/cards` | Карты в шаблоне |
| CRUD | `/api/reading` | Гадания (+ filter status/from/to, sort, pagination) |
| GET | `/api/users/{userId}/readings` | Гадания пользователя |
| GET | `/api/stats/cards-by-suit` | Агрегация по мастям |
| GET | `/api/stats/readings-by-status` | Агрегация по статусам |
| GET | `/api/stats/deck-usage` | JOIN: использование колод |
| GET | `/api/stats/spread-stats` | JOIN: статистика шаблонов |
| GET/POST | `/api/partitions/*` | Управление/проверка партиций |

Примеры фильтрации и пагинации:

```
GET /api/card?page=1&pageSize=20&search=cup&suit=Cups&sort=-created_at
GET /api/reading?page=1&pageSize=20&status=COMPLETED&from=2026-01-01&sort=-created_at
```

## Сложные запросы

### JOIN 1 — использование колод в гаданиях
`GET /api/stats/deck-usage`

```sql
SELECT d.id, d.name, COUNT(DISTINCT c.id), COUNT(DISTINCT r.id)
FROM decks d
LEFT JOIN cards c ON c.deck_id = d.id
LEFT JOIN reading_cards rc ON rc.card_id = c.id
LEFT JOIN readings r ON r.id = rc.reading_id
GROUP BY d.id, d.name;
```

### JOIN 2 — детали гадания (карта + колода + шаблон)
Используется в `ReadingRepository.GetByIdAsync` (Include Spread/User/Cards/Deck).

### Агрегация — карты по мастям
`GET /api/stats/cards-by-suit`

```sql
SELECT suit, COUNT(*) AS cards_count
FROM cards
GROUP BY suit
ORDER BY cards_count DESC;
```

Также: `GET /api/stats/readings-by-status`, `GET /api/stats/spread-stats`.

## Генерация данных

Небольшой seed уже в Liquibase (`002-readings-and-seed.xml`).

Массовая генерация:

```bash
docker exec -i db_primary psql -U test_project_db -d test_project_db < Scripts/generate_data.sql
```

В скрипте `\set count 100000` — можно увеличить до 1_000_000+.

## Партиционирование (Lab 3)

Таблица `readings` партиционирована по `RANGE (created_at)` (месячные партиции).

- `CreatePartitionsJob` — создаёт недостающие партиции на горизонт 3 месяца;
- `PartitionHealthCheckService` — проверяет наличие партиций и шлёт alert/recovery;
- алерты пишутся в лог и файлы `alerts/`, опционально email (`Alerts:Email` в appsettings).

Ручная проверка:

```
POST /api/partitions/ensure
GET  /api/partitions/health
GET  /api/partitions
```

## Read Scaling (Lab 4)

Streaming replication: `db_primary` → `db_replica`.

- Запись (INSERT/UPDATE/DELETE) — `ConnectionStrings:DefaultConnection` → Primary  
- `GET /api/reading*` — `ConnectionStrings:ReplicaConnection` → `TarotReadDbContext` → Replica  
- Скрипты: `Scripts/replication/`

## Лабораторные работы (через pgAdmin)

Подробная инструкция: **[PGADMIN.md](PGADMIN.md)**

| Папка | SQL для Query Tool | Тема |
|-------|-------------------|------|
| `lab1/` | `pgadmin_lab1.sql` | Индексы и EXPLAIN ANALYZE |
| `lab2/` | `pgadmin_lab2.sql` | Рост данных |
| `lab3/` | `pgadmin_lab3.sql` | Партиционирование |
| `lab4/` | `pgadmin_lab4.sql` | Primary + Replica / Read Scaling |

В каждой папке: `REPORT.md`, `контрольные_вопросы.md`, `pgadmin_labN.sql`.

Как презентовать преподавателю: **[КАК_СДАТЬ.md](КАК_СДАТЬ.md)**

Подключения в pgAdmin:
- Primary `localhost:5437`
- Replica `localhost:5438`  
Импорт: File → Import/Export Servers → `pgadmin_servers.json`.

## Стек

- C# / ASP.NET Core 8
- PostgreSQL 16 (Primary + Replica)
- Redis 7
- Liquibase
- EF Core + Dapper
- Docker Compose
- Swagger / OpenAPI
- xUnit
