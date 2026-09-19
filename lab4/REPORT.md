# Лабораторная работа №4  
# Масштабирование чтения: Primary + Replica

**Проект:** Tarot Reading Service  
**Primary:** Docker `db_primary` → `localhost:5437`  
**Replica:** Docker `db_replica` → `localhost:5438`  
**Репликация:** PostgreSQL streaming replication (async)  
**Код Read Scaling:**
- `ConnectionStrings:ReplicaConnection` (`appsettings.json`)
- `Data/TarotReadDbContext.cs`
- `Repositories/ReadingRepository.cs` — GET идут на Replica, POST/PUT/DELETE на Primary
- `Scripts/replication/` — init Primary + entrypoint Replica

SQL для pgAdmin: `lab4/pgadmin_lab4.sql`

---

## Часть 1. Primary и Replica в окружении

Фрагмент `docker-compose.yml`:

| Сервис | Роль | Порт хоста | Контейнер |
|--------|------|------------|-----------|
| `db_primary` | Primary (read-write) | **5437** | `db_primary` |
| `db_replica` | Replica (read-only) | **5438** | `db_replica` |

Primary поднимается с `wal_level=replica`, `max_wal_senders=10`, init-скрипт создаёт роль `replicator`.  
Replica делает `pg_basebackup -R` с Primary и стартует в hot standby.

Подключение:

```
Primary: Host=localhost; Port=5437; Database=test_project_db; User/Password=test_project_db
Replica: Host=localhost; Port=5438; Database=test_project_db; User/Password=test_project_db
```

Проверка роли:

```sql
SELECT pg_is_in_recovery();  -- Primary: f | Replica: t
```

Факт эксперимента: на Replica `pg_is_in_recovery() = t`.

---

## Часть 2. Streaming replication

Цепочка:

1. Primary изменяет данные и пишет в **WAL**  
2. `walsender` отдаёт поток WAL  
3. на Replica `walreceiver` принимает поток  
4. standby **replay** применяет изменения  

На Primary:

```text
 application_name |   state   | sync_state | client_addr
------------------+-----------+------------+-------------
 walreceiver      | streaming | async      | 172.18.0.3
```

`state = streaming`, `sync_state = async` — асинхронная физическая репликация.

---

## Часть 3. Доказательство репликации

**На Primary:**

```sql
CREATE TABLE IF NOT EXISTS lab4_demo (...);
INSERT INTO lab4_demo(note) VALUES ('hello-from-primary') RETURNING *;
-- id=1, note=hello-from-primary
```

**На Replica (сразу после):**

```text
 id |        note
----+--------------------
  1 | hello-from-primary
```

Строка, вставленная на Primary, видна на Replica → WAL доставлен и применён.

---

## Часть 4. Read-only поведение Replica

```sql
-- на Replica:
INSERT INTO lab4_demo(note) VALUES ('should-fail-on-replica');
```

Результат:

```text
ERROR:  cannot execute INSERT in a read-only transaction
```

Replica нельзя использовать как независимую БД для записи приложения: она копия в recovery mode. Все INSERT/UPDATE/DELETE — только на Primary.

---

## Часть 5. Чтение сервиса с Replica

В backend добавлено второе подключение:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=db_primary;...",
  "ReplicaConnection": "Host=db_replica;..."
}
```

- `TarotDbContext` → Primary (запись)  
- `TarotReadDbContext` → Replica (чтение)  

В `ReadingRepository`:

| Метод | Контекст | Узел |
|-------|----------|------|
| `GetPagedAsync` / `GetByIdAsync` / `GetByUserIdAsync` | `_read` (`TarotReadDbContext`) | **Replica** |
| `CreateAsync` / `UpdateAsync` / `DeleteAsync` | `_write` (`TarotDbContext`) | **Primary** |

Реальный read-сценарий: **`GET /api/reading`** (список раскладов) обслуживается с Replica.  
Запись **`POST /api/reading`** по-прежнему на Primary. Liquibase мигрирует только Primary; схема на Replica появляется через streaming.

---

## Часть 6. Replication lag

Запрос на Primary:

```sql
SELECT application_name, state, sync_state,
       pg_wal_lsn_diff(pg_current_wal_lsn(), replay_lsn) AS replay_lag_bytes
FROM pg_stat_replication;
```

В эксперименте при лёгкой нагрузке `replay_lag_bytes = 0` — Replica успевала сразу.  
Это нормально для локального Docker. Главный вывод задания:

> Репликация **не** означает мгновенную синхронизацию. Между COMMIT на Primary и replay на Replica возможен **replication lag**; следующий SELECT на Replica теоретически может увидеть ещё старые данные (eventual consistency).

У нас асинхронный режим (`async`) — приложение не ждёт подтверждения от Replica при записи.

---

## Итог

| Требование | Статус |
|------------|--------|
| Primary + Replica в docker-compose | ✓ |
| Streaming replication | ✓ `streaming` / `async` |
| Запись на Primary видна на Replica | ✓ `lab4_demo` |
| INSERT на Replica запрещён | ✓ read-only |
| Read-сценарий сервиса на Replica | ✓ `GET /api/reading` |
| Понимание lag | ✓ async + `pg_stat_replication` |

Контрольные вопросы: `lab4/контрольные_вопросы.md`.
