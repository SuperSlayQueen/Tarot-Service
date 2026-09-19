# Лабораторная работа №3  
# Партиционирование PostgreSQL

**Миграция сервиса:** `Data/Liquibase/changesets/004-readings-partitioning.xml`  
**Код автоматизации:**
- `Services/PartitionService.cs`
- `Services/PartitionJobs.cs` (`CreatePartitionsJob`, `PartitionHealthCheckService`)
- `Services/AlertService.cs`
- `Controllers/PartitionController.cs`  

---

## Часть 1. RANGE по дате (`lab3_events`)

Созданы партиции на 2026-09-09 … 2026-09-11. Распределение:

| partition | count |
|-----------|------:|
| `lab3_events_2026_09_09` | 1 |
| `lab3_events_2026_09_10` | 2 |
| `lab3_events_2026_09_11` | 1 |

**Ответы:**
1. `created_at = '2026-09-10 12:00:00'` → `lab3_events_2026_09_10`
2. `created_at = '2026-09-11 00:00:00'` → `lab3_events_2026_09_11` (граница `TO` **не** включает нижнюю дату следующей партиции; `FROM` включительно, `TO` исключительно)
3. Вставка за `2026-09-12` без подходящей/DEFAULT партиции → **ERROR** (проверено):
   `no partition of relation "lab3_events" found for row` / `created_at = (2026-09-12 00:00:00)`
4. Полуинтервал `[FROM, TO)` — стандарт declarative partitioning

## Часть 2. Partition pruning

Запрос за один день (`>= 2026-09-10 AND < 2026-09-11`):
- в плане только **`Seq Scan on lab3_events_2026_09_10`**
- остальные партиции исключены (pruning)
- Execution Time **0.026 ms**

Запрос `WHERE event_type = 'click'`:
- **Append** по всем трём партициям
- pruning нет, потому что фильтр не по partition key

## Часть 3. RANGE по цене (`lab3_products`)

Партиции: cheap `[0,100)`, medium `[100,1000)`, expensive `[1000, MAXVALUE)`.

Запрос `price >= 100 AND price < 500` должен затронуть только **medium** (подтверждено планом — Seq Scan на medium-партиции).

## Часть 4–5. LIST + DEFAULT

- `WHERE customer_type = 'B2B'` → только `lab3_customers_b2b`
- LIST отличается тем, что режет по дискретному множеству значений, а не по диапазону
- INSERT `VIP` без DEFAULT → ошибка
- После `DEFAULT` partition строка размещается в `lab3_customers_default`

DEFAULT полезен как страховка, но опасен как «свалка»: теряется pruning, растёт неучтённая партиция, сложнее сопровождение.

## Часть 6. HASH (`lab3_user_events`, 100k строк, MODULUS 4)

| partition | count |
|-----------|------:|
| `_0` | 24838 |
| `_1` | 25311 |
| `_2` | 25203 |
| `_3` | 24648 |

Распределение близко к равномерному (±2%).  
HASH полезен для балансировки по `user_id`.  
Плохо подходит для «удалить старше 3 лет» — данные одного периода размазаны по всем bucket’ам, drop одной партиции не равен TTL.

## Часть 7. Выбор стратегии

| Сценарий | Стратегия | Почему |
|----------|-----------|--------|
| A. Миллионы событий/день, удалять старше 3 лет | **RANGE(created_at)** | drop старых партиций = быстрый TTL |
| B. B2C/B2B/Enterprise | **LIST(customer_type)** | запросы фильтруют по типу |
| C. Равномерно по user_id | **HASH(user_id)** | балансировка без диапазона |
| D. Аналитика по `created_at BETWEEN` | **RANGE(created_at)** | pruning по времени |
| E. Платежи по странам EE/LV/LT/FI/SE | **LIST(country)** | небольшое фиксированное множество |

## Часть 8–9. Индексы + когда partitioning не помогает

- Индекс по `user_id` на parent создаёт локальные индексы на партициях.
- Запрос с датой + `user_id` выигрывает от **pruning + index**.
- `WHERE event_type = 'click'` даже с индексом по типу всё равно идёт в **Append по всем партициям** — partitioning не решает задачу без ключа в фильтре.
- **Вывод:** partitioning режет объём по ключу; индекс ускоряет поиск внутри выбранных секций.

## Часть 10–11. Автоматизация и alerting (в сервисе)

### CreatePartitionsJob
Фоновый `BackgroundService` раз в час:
1. берёт текущий месяц;
2. требует горизонт **текущий + 3 месяца**;
3. читает существующие `readings_*`;
4. создаёт недостающие `CREATE TABLE ... PARTITION OF readings ...`;
5. логирует Existing/Required/Missing/Created.

### PartitionHealthCheckService
Каждые 5 минут:
- OK → recovery alert (если раньше был CRITICAL);
- CRITICAL → alert с списком missing partitions;
- дедупликация одинаковых CRITICAL в `AlertService`.

Каналы alert:
- лог приложения;
- файлы в каталоге `alerts/` при работе сервиса;
- опционально email (`Alerts:Email` в appsettings).

API для демонстрации:
```
POST /api/partitions/ensure
GET  /api/partitions/health
GET  /api/partitions
```

### Как проверить alert (выполненный demo)
1. Созданы будущие партиции `readings_*`.
2. Удалена пустая `readings_2026_12` (`DROP TABLE`).
3. Зафиксирован CRITICAL: `demo_CRITICAL.txt`.
4. Партиция пересоздана → recovery: `demo_RECOVERY.txt`.
5. Повторный CRITICAL при всё ещё missing подавляется дедупликацией в `AlertService`.

Ручной API-вариант: `POST /api/partitions/ensure`, `GET /api/partitions/health`.

---

## Часть 12. Партиционирование собственного сервиса

### Выбор
| Шаг | Решение |
|-----|---------|
| Таблица | `readings` |
| Ключ | `created_at` |
| Стратегия | **RANGE** (месячные партиции) |
| Почему | естественный рост во времени; API фильтрует `from`/`to`; удобно архивировать старые месяцы |

### Партиции (факт после эксперимента)

```
readings_2026_08
readings_2026_09
readings_2026_10
readings_2026_11
readings_2026_12
readings_default
```

### Проверка запросов API

| Запрос | Pruning? | Комментарий |
|--------|----------|-------------|
| `GET /api/reading?from=&to=` (месяц) | **Да** | Seq/Index только на нужных партициях (~0.04 ms на LIMIT) |
| `GET /api/reading?status=NEW` | Нет по дате | Append + index по status |
| `GET /api/stats/readings-by-status` | Нет | агрегация по всем секциям (~93 ms на 1M) |

### Автоматизация / контроль / alert
Реализованы в приложении (см. выше). Миграция `004-readings-partitioning.xml` конвертирует таблицу при `docker compose up`.

---

## Итог

Партиционирование — это не только `PARTITION BY`, а жизненный цикл: проектирование ключа → создание → pruning → автосоздание будущих секций → health-check → alert → recovery.

Контрольные вопросы: `lab3/контрольные_вопросы.md`.
