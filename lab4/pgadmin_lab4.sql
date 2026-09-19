-- Lab 4. Read Scaling: Primary + Replica
-- Выполняйте блоки по одному (от -- >>> до следующего).
-- Primary: localhost:5437  |  Replica: localhost:5438
-- В pgAdmin заведите ДВА сервера (см. PGADMIN.md / pgadmin_servers.json).

-- >>> Часть 1. Кто я: Primary или Replica?
SELECT inet_server_addr() AS server_addr,
       inet_server_port() AS server_port,
       pg_is_in_recovery() AS is_replica,
       CASE WHEN pg_is_in_recovery() THEN 'Replica (read-only)' ELSE 'Primary (read-write)' END AS role;

-- >>> Часть 2. Состояние streaming replication (ТОЛЬКО на Primary)
SELECT application_name,
       client_addr,
       state,
       sync_state,
       sent_lsn,
       write_lsn,
       flush_lsn,
       replay_lsn,
       pg_wal_lsn_diff(pg_current_wal_lsn(), replay_lsn) AS replay_lag_bytes
FROM pg_stat_replication;

-- >>> Часть 3a. Запись на Primary
CREATE TABLE IF NOT EXISTS lab4_demo (
    id          serial PRIMARY KEY,
    note        text NOT NULL,
    created_at  timestamptz NOT NULL DEFAULT now()
);

INSERT INTO lab4_demo(note)
VALUES ('lab4-primary-' || to_char(clock_timestamp(), 'HH24:MI:SS.MS'))
RETURNING id, note, created_at;

-- >>> Часть 3b. Чтение той же строки на Replica (подключитесь к порту 5438)
SELECT id, note, created_at
FROM lab4_demo
ORDER BY id DESC
LIMIT 5;

-- >>> Часть 4. INSERT на Replica должен упасть (read-only)
-- Ожидаемо: ERROR: cannot execute INSERT in a read-only transaction
INSERT INTO lab4_demo(note) VALUES ('should-fail-on-replica');

-- >>> Часть 5. Replication lag (на Primary)
SELECT application_name,
       state,
       sync_state,
       pg_wal_lsn_diff(pg_current_wal_lsn(), sent_lsn)   AS send_lag_bytes,
       pg_wal_lsn_diff(pg_current_wal_lsn(), replay_lsn) AS replay_lag_bytes
FROM pg_stat_replication;

-- >>> Часть 6. Сервисные readings уже на обеих нодах после Liquibase
SELECT COUNT(*) AS readings_count FROM readings;

-- На Primary: INSERT / UPDATE readings
-- В приложении: GET /api/reading → ReplicaConnection (TarotReadDbContext)
