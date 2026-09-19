-- LAB 2 — Рост данных

-- >>> Блок 1. Создание таблицы events
DROP TABLE IF EXISTS lab2_events CASCADE;

CREATE TABLE lab2_events (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    payload JSONB,
    created_at TIMESTAMP NOT NULL
);

-- >>> Блок 2a. 10 000 строк
INSERT INTO lab2_events (user_id, event_type, payload, created_at)
SELECT
    (random() * 100000)::bigint,
    CASE
        WHEN random() < 0.4 THEN 'MESSAGE'
        WHEN random() < 0.7 THEN 'LOGIN'
        WHEN random() < 0.9 THEN 'PURCHASE'
        ELSE 'OTHER'
    END,
    '{}'::jsonb,
    NOW() - (random() * INTERVAL '365 days')
FROM generate_series(1, 10000);
ANALYZE lab2_events;

SELECT COUNT(*) AS cnt,
       pg_size_pretty(pg_relation_size('lab2_events')) AS table_size,
       pg_size_pretty(pg_total_relation_size('lab2_events')) AS total_size
FROM lab2_events;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab2_events WHERE user_id = 123;

-- >>> Блок 2b. до 100 000
INSERT INTO lab2_events (user_id, event_type, payload, created_at)
SELECT
    (random() * 100000)::bigint,
    CASE WHEN random() < 0.4 THEN 'MESSAGE' WHEN random() < 0.7 THEN 'LOGIN'
         WHEN random() < 0.9 THEN 'PURCHASE' ELSE 'OTHER' END,
    '{}'::jsonb,
    NOW() - (random() * INTERVAL '365 days')
FROM generate_series(1, 90000);
ANALYZE lab2_events;

SELECT COUNT(*) AS cnt,
       pg_size_pretty(pg_relation_size('lab2_events')) AS table_size,
       pg_size_pretty(pg_total_relation_size('lab2_events')) AS total_size
FROM lab2_events;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab2_events WHERE user_id = 123;

-- >>> Блок 2c. до 1 000 000
INSERT INTO lab2_events (user_id, event_type, payload, created_at)
SELECT
    (random() * 100000)::bigint,
    CASE WHEN random() < 0.4 THEN 'MESSAGE' WHEN random() < 0.7 THEN 'LOGIN'
         WHEN random() < 0.9 THEN 'PURCHASE' ELSE 'OTHER' END,
    '{}'::jsonb,
    NOW() - (random() * INTERVAL '365 days')
FROM generate_series(1, 900000);
ANALYZE lab2_events;

SELECT COUNT(*) AS cnt,
       pg_size_pretty(pg_relation_size('lab2_events')) AS table_size,
       pg_size_pretty(pg_total_relation_size('lab2_events')) AS total_size
FROM lab2_events;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab2_events WHERE user_id = 123;

-- >>> Блок 2d. до 5 000 000 (долго: 1–3 минуты)
INSERT INTO lab2_events (user_id, event_type, payload, created_at)
SELECT
    (random() * 100000)::bigint,
    CASE WHEN random() < 0.4 THEN 'MESSAGE' WHEN random() < 0.7 THEN 'LOGIN'
         WHEN random() < 0.9 THEN 'PURCHASE' ELSE 'OTHER' END,
    '{}'::jsonb,
    NOW() - (random() * INTERVAL '365 days')
FROM generate_series(1, 4000000);
ANALYZE lab2_events;

SELECT COUNT(*) AS cnt,
       pg_size_pretty(pg_relation_size('lab2_events')) AS table_size,
       pg_size_pretty(pg_total_relation_size('lab2_events')) AS total_size
FROM lab2_events;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab2_events WHERE user_id = 123;

-- >>> Блок 5. Индекс user_id
CREATE INDEX idx_lab2_events_user_id ON lab2_events(user_id);
ANALYZE lab2_events;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab2_events WHERE user_id = 123;

-- >>> Блок 6. Диапазон дат
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab2_events WHERE created_at >= NOW() - INTERVAL '1 day';

CREATE INDEX idx_lab2_events_created_at ON lab2_events(created_at);
ANALYZE lab2_events;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab2_events WHERE created_at >= NOW() - INTERVAL '1 day';

-- >>> Блок 7. Фильтр + сортировка
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab2_events
WHERE user_id = 123
ORDER BY created_at DESC
LIMIT 100;

CREATE INDEX idx_lab2_events_user_created ON lab2_events(user_id, created_at DESC);
ANALYZE lab2_events;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab2_events
WHERE user_id = 123
ORDER BY created_at DESC
LIMIT 100;

-- >>> Блок 8. Агрегация
EXPLAIN (ANALYZE, BUFFERS)
SELECT event_type, COUNT(*)
FROM lab2_events
WHERE created_at >= NOW() - INTERVAL '30 days'
GROUP BY event_type;

-- >>> Блок 9. Стоимость индексов на INSERT
DROP TABLE IF EXISTS lab2_write_bench;
CREATE TABLE lab2_write_bench (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    payload JSONB,
    created_at TIMESTAMP NOT NULL
);

-- без индексов (запомните время в Messages)
INSERT INTO lab2_write_bench(user_id, event_type, payload, created_at)
SELECT (random()*100000)::bigint, 'LOGIN', '{}'::jsonb, NOW()
FROM generate_series(1, 200000);

TRUNCATE lab2_write_bench;
CREATE INDEX idx_wb_user ON lab2_write_bench(user_id);
CREATE INDEX idx_wb_created ON lab2_write_bench(created_at);
CREATE INDEX idx_wb_user_created ON lab2_write_bench(user_id, created_at DESC);

-- с индексами
INSERT INTO lab2_write_bench(user_id, event_type, payload, created_at)
SELECT (random()*100000)::bigint, 'LOGIN', '{}'::jsonb, NOW()
FROM generate_series(1, 200000);

-- >>> Блок 10. Размер индексов
SELECT indexrelname,
       pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
FROM pg_stat_user_indexes
WHERE relname = 'lab2_events';

-- >>> Блок 11. Когда индекс не спасает (письменный разбор + план)
EXPLAIN (ANALYZE, BUFFERS)
SELECT DATE(created_at), COUNT(*)
FROM lab2_events
WHERE created_at >= NOW() - INTERVAL '365 days'
GROUP BY DATE(created_at);

-- >>> Блок 12–18. Свой сервис (readings)
SELECT COUNT(*) AS readings_count FROM readings;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM readings WHERE user_id = 1;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM readings
WHERE created_at >= NOW() - INTERVAL '7 days';

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM readings
WHERE user_id = 1
ORDER BY created_at DESC
LIMIT 50;

-- Опционально: догнать readings до нужного объёма (раскомментируйте)
/*
WITH u AS (SELECT array_agg(id) ids FROM users),
     s AS (SELECT array_agg(id) ids FROM spreads)
INSERT INTO readings(user_id, spread_id, question, status, created_at)
SELECT u.ids[1+(floor(random()*array_length(u.ids,1)))::int],
       s.ids[1+(floor(random()*array_length(s.ids,1)))::int],
       'pgadmin-gen',
       (ARRAY['NEW','COMPLETED','CANCELLED'])[1+(floor(random()*3))::int],
       NOW()-(random()*INTERVAL '365 days')
FROM generate_series(1, 100000) g, u, s;
ANALYZE readings;
*/
