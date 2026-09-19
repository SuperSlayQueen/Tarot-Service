# Лабораторная работа №1  
# Индексы и EXPLAIN ANALYZE в PostgreSQL

**Проект:** Tarot Reading Service  
**БД:** PostgreSQL 16 (Docker `db_primary`, порт 5437)  
**Миграция индексов сервиса:** `Data/Liquibase/changesets/003-lab1-indexes.xml`  

---

## Часть 1. Работа с тестовой базой (`lab1_orders`, 1 000 000 строк)

### Задание 3–4. EXPLAIN / EXPLAIN ANALYZE

Запрос: `SELECT * FROM lab1_orders WHERE user_id = 123;`

**До индекса:**
- План: `Parallel Seq Scan`
- Execution Time: **24.126 ms**
- Rows Removed by Filter ≈ 333 329 на воркер

**Выводы:**
1. PostgreSQL выбрал последовательное (параллельное) сканирование — индекса по `user_id` ещё нет.
2. EXPLAIN показывает только оценку; EXPLAIN ANALYZE реально выполняет запрос и даёт actual time/rows.
3. EXPLAIN ANALYZE дольше, потому что читает таблицу целиком.

### Задание 5. Sequential Scan

| Запрос | Scan | Execution Time |
|--------|------|----------------|
| `SELECT * FROM lab1_orders` | Seq Scan | 66.662 ms |
| `WHERE amount > 0` | Seq Scan | 106.479 ms |

Seq Scan здесь рационален: нужна почти вся таблица. Индекс не ускорил бы полный/почти полный проход.

### Задание 6. Первый B-tree индекс

```sql
CREATE INDEX idx_lab1_orders_user_id ON lab1_orders(user_id);
```

| Метрика | До | После |
|---------|----|-------|
| Тип Scan | Parallel Seq Scan | Bitmap Index Scan → Bitmap Heap Scan |
| Execution Time | 24.126 ms | **0.077 ms** |
| Индекс | — | `idx_lab1_orders_user_id` |

План изменился, индекс используется, ускорение ~300×.

### Задание 7–8. Индекс и селективность (`status`)

Распределение почти равномерное (~25% на каждый статус):

| status | count |
|--------|------:|
| CANCELLED | 249900 |
| DELIVERED | 249773 |
| NEW | 250145 |
| PAID | 250182 |

Для всех статусов PostgreSQL выбрал **Bitmap Index/Heap Scan** (~33–36 ms).  
При низкой селективности (~25% таблицы) Bitmap предпочтительнее обычного Index Scan; Seq Scan тоже мог бы конкурировать на ещё больших долях.

**Вывод:** чем ниже селективность, тем меньше выигрыш от индекса (и тем чаще Bitmap/Seq).

### Задание 9. Range по `created_at`

| Диапазон | До индекса | После индекса |
|----------|------------|---------------|
| 7 days | Parallel Seq 45.2 ms | Bitmap **8.97 ms** |
| 1 day | — | Bitmap **1.34 ms** |
| 1 month | — | Bitmap **14.5 ms** |
| 1 year | — | **Seq Scan 144 ms** |

Индекс выгоден для узкого окна; для года (~половины данных за 2 года) планировщик снова выбирает Seq Scan.

### Задание 10–12. Bitmap / несколько индексов / composite

- `amount BETWEEN 1000 AND 3000` без узкой селективности → Seq Scan (~86 ms).
- `user_id = 123 AND status = 'PAID'` при наличии узкого индекса по `user_id`: один Bitmap Index + Filter по status (**0.068 ms**) — **BitmapAnd не понадобился**, потому что `user_id` уже очень селективен.
- Чтобы увидеть **BitmapAnd**, расширили условие: `user_id < 500 AND status = 'PAID'` → в плане `BitmapAnd` двух Bitmap Index Scan (user_id + status), **5.37 ms**.
- После `CREATE INDEX (user_id, status)`: обычный **Index Scan, 0.039 ms** — составной индекс эффективнее объединения двух.

Составной индекс лучше, когда условия часто идут вместе; отдельные индексы гибче для независимых фильтров.

### Задание 13. Порядок колонок

- `(user_id, created_at)` помогает запросам с `user_id`.
- Запрос только по `created_at` использует индекс `(created_at, …)` / `created_at`, но не ведущий `user_id`.
- `(user_id, created_at)` и `(created_at, user_id)` — разные индексы по правилу leftmost prefix.

### Задание 14–15. ORDER BY + LIMIT (pagination)

Запрос API-подобного вида:

```sql
SELECT * FROM lab1_orders
WHERE user_id = 123
ORDER BY created_at DESC
LIMIT 20;
```

Создан индекс `(user_id, created_at DESC)`.  
В измерениях Sort иногда остаётся из‑за выбора Bitmap-плана, но стоимость низкая (~0.08 ms). Для гарантированного отсутствия Sort важен Index Scan по покрывающему порядку ключей и достаточная селективность `user_id`.

### Задание 16–18. Index Only / Partial / Expression

- После `VACUUM ANALYZE` и индекса `INCLUDE (id, status, created_at)` получен **Index Only Scan**, `Heap Fetches: 0`, **0.033 ms**.
- Partial index `WHERE status = 'NEW'` уменьшает размер индекса под «горячий» статус.
- `LOWER(email)`: обычный индекс **не** использовался (Parallel Seq **27.9 ms**); expression index → Index Scan **0.037 ms**.

### Задание 19. Цена индексов на INSERT

| Сценарий | Время вставки 200 000 строк |
|----------|-----------------------------|
| Без доп. индексов | **0.342 s** |
| С 3 индексами | **0.846 s** (~2.5× медленнее) |

Каждый INSERT обновляет все индексы → дороже запись и WAL.

### Задание 20. Статистика индексов (`pg_stat_user_indexes`)

Часть индексов с `idx_scan = 0` сразу после создания (например `idx_lab1_orders_new`, `idx_lab1_orders_created_at_user`) — ещё не использовались или перекрыты более подходящими.  
`idx_scan = 0` ≠ автоматически «удалить»: индекс мог быть создан недавно, нужен для редких отчётов/уникальности. Перед drop — анализ нагрузки.

### Задание 21. Финальная оптимизация тестового запроса

```sql
CREATE INDEX idx_lab1_final ON lab1_orders(user_id, status, created_at DESC);
```

Запрос `user_id + status=PAID + created_at >= now()-30d ORDER BY created_at DESC LIMIT 50`:

| | До (уже были частичные индексы) | После финального |
|--|--------------------------------|------------------|
| Scan | Index Scan | Index Scan |
| Time | 0.055 ms | **0.036 ms** |

Порядок колонок: равенства (`user_id`, `status`) слева, диапазон/сортировка (`created_at`) справа.

---

## Часть 2. Работа со своим сервисом

### Scaling Entity

1. **Таблица:** `readings`
2. **Смысл:** пользовательские сессии гадания (вопрос, статус, выбранные карты).
3. **Почему растёт быстрее:** каждое обращение клиента создаёт запись; `cards`/`decks`/`spreads` почти статичны.
4. **Оценка через год:** при активной аудитории — миллионы–десятки миллионов строк.
5. **Частые фильтры:** `user_id`, `status`, `created_at`, комбинации + `ORDER BY created_at DESC LIMIT n`.

### Исследуемые запросы (на ~300k…1M readings)

| Запрос | Суть | Scan (типично) | Execution Time |
|--------|------|----------------|----------------|
| Q1 | `WHERE user_id = 1` | Append + Bitmap/Seq по партициям | ~23.7 ms (300k) → ~93 ms (1M) |
| Q2 | `user_id + status` | Index/Bitmap по партициям | быстрее точечного полного user dump |
| Q3 | `user_id ORDER BY created_at DESC LIMIT 20` | Index Scan на партициях | ~0.2–0.3 ms |

### Индексы, добавленные в проект (миграции)

```sql
CREATE INDEX idx_readings_user_status_created ON readings (user_id, status, created_at DESC);
CREATE INDEX idx_readings_completed_created ON readings (created_at DESC) WHERE status = 'COMPLETED';
CREATE INDEX idx_cards_suit ON cards (suit);
CREATE INDEX idx_cards_name_lower ON cards (LOWER(name));
```

Файл: `Data/Liquibase/changesets/003-lab1-indexes.xml`.

### Оптимизация (задания 27–29)

Выбран запрос ленты гаданий пользователя (endpoint `GET /api/reading?userId=&sort=-created_at`):

```sql
SELECT * FROM readings
WHERE user_id = $1
ORDER BY created_at DESC
LIMIT 20;
```

Индекс `(user_id, created_at DESC)` / `(user_id, status, created_at DESC)` переводит план к Index Scan по партициям и держит время около **0.2 ms** даже на миллионе строк (см. Lab 2 svc Q3).

### Задание 30. Почему нельзя индексировать всё?

Индексы занимают место (на `lab2_events` одни только вторичные индексы дают сотни MB), замедляют INSERT/UPDATE/DELETE, увеличивают WAL и vacuum. Часть индексов почти не используется, но платится при каждой записи. Индекс оправдан измеренным запросом, а не «на всякий случай».

---

## Артефакты

- Контрольные вопросы: `контрольные_вопросы.md`
- Индексы сервиса: `Data/Liquibase/changesets/003-lab1-indexes.xml`
