# Работа с лабораторными через pgAdmin 4

## 1. Запуск PostgreSQL

В корне проекта:

```bash
docker compose up -d db_primary db_replica redis
docker compose up liquibase
```

- **Primary:** порт **5437** (`db_primary`)
- **Replica:** порт **5438** (`db_replica`) — нужна для Lab 4

## 2. Открыть pgAdmin

Пуск → **pgAdmin 4**

При первом запуске задайте master password (свой пароль только для pgAdmin).

## 3. Создать подключения (Register → Server)

Или импорт: File → Import/Export Servers → `pgadmin_servers.json`.

| Имя | Host | Port | DB / User / Password |
|-----|------|------|----------------------|
| Tarot Primary | `localhost` | **5437** | `test_project_db` |
| Tarot Replica | `localhost` | **5438** | `test_project_db` |

Путь: **Servers → … → Databases → test_project_db → Schemas → public**

## 4. Как выполнять SQL

1. ПКМ по `test_project_db` → **Query Tool**
2. **Open File** → скрипт лабы:
   - `lab1/pgadmin_lab1.sql`
   - `lab2/pgadmin_lab2.sql`
   - `lab3/pgadmin_lab3.sql`
   - `lab4/pgadmin_lab4.sql` (блоки на Primary и на Replica — разные серверы)
3. Выделяйте **один блок** (от `-- >>>` до следующего) и жмите **Execute (F5)**

> Не запускайте весь файл целиком — тяжёлые INSERT пойдут подряд.

## 5. Порядок сдачи

1. Lab 1 → `lab1/REPORT.md`
2. Lab 2 → `lab2/REPORT.md`
3. Lab 3 → `lab3/REPORT.md`
4. Lab 4 → `lab4/REPORT.md` (+ показать `pg_stat_replication` и read-only ошибку на Replica)

Контрольные вопросы: `labN/контрольные_вопросы.md`

## 6. Скриншоты

В отчётах **нет** папки screenshots — опирайтесь на текст отчёта и живой вывод в Query Tool / терминале.
