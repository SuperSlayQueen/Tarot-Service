-- LAB 3 — Партиционирование 


-- >>> Часть 1. RANGE по дате
DROP TABLE IF EXISTS lab3_events CASCADE;

CREATE TABLE lab3_events (
    id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    event_type VARCHAR(50) NOT NULL,
    payload TEXT,
    created_at TIMESTAMP NOT NULL
) PARTITION BY RANGE (created_at);

CREATE TABLE lab3_events_2026_09_09
    PARTITION OF lab3_events FOR VALUES FROM ('2026-09-09') TO ('2026-09-10');
CREATE TABLE lab3_events_2026_09_10
    PARTITION OF lab3_events FOR VALUES FROM ('2026-09-10') TO ('2026-09-11');
CREATE TABLE lab3_events_2026_09_11
    PARTITION OF lab3_events FOR VALUES FROM ('2026-09-11') TO ('2026-09-12');

INSERT INTO lab3_events VALUES
(1, 10, 'click', '{}', '2026-09-09 10:00'),
(2, 11, 'click', '{}', '2026-09-10 12:00'),
(3, 12, 'view',  '{}', '2026-09-11 08:00'),
(4, 13, 'click', '{}', '2026-09-10 23:59');

SELECT tableoid::regclass AS partition_name, COUNT(*)
FROM lab3_events
GROUP BY tableoid
ORDER BY partition_name;

-- Ожидаемая ошибка (нет партиции на 2026-09-12) — выполните отдельно:
-- INSERT INTO lab3_events VALUES (99, 1, 'x', '{}', '2026-09-12 00:00');

-- >>> Часть 2. Partition pruning
EXPLAIN (ANALYZE, BUFFERS)
SELECT COUNT(*)
FROM lab3_events
WHERE created_at >= '2026-09-10' AND created_at < '2026-09-11';

EXPLAIN (ANALYZE, BUFFERS)
SELECT COUNT(*)
FROM lab3_events
WHERE event_type = 'click';

-- >>> Часть 3. RANGE по цене
DROP TABLE IF EXISTS lab3_products CASCADE;

CREATE TABLE lab3_products (
    id BIGINT NOT NULL,
    name TEXT NOT NULL,
    price NUMERIC NOT NULL
) PARTITION BY RANGE (price);

CREATE TABLE lab3_products_cheap
    PARTITION OF lab3_products FOR VALUES FROM (0) TO (100);
CREATE TABLE lab3_products_medium
    PARTITION OF lab3_products FOR VALUES FROM (100) TO (1000);
CREATE TABLE lab3_products_expensive
    PARTITION OF lab3_products FOR VALUES FROM (1000) TO (MAXVALUE);

INSERT INTO lab3_products VALUES (1, 'A', 50), (2, 'B', 250), (3, 'C', 1500);

SELECT tableoid::regclass AS partition_name, id, name, price
FROM lab3_products
ORDER BY id;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab3_products WHERE price >= 100 AND price < 500;

-- >>> Часть 4–5. LIST + DEFAULT
DROP TABLE IF EXISTS lab3_customers CASCADE;

CREATE TABLE lab3_customers (
    id BIGINT NOT NULL,
    name TEXT NOT NULL,
    customer_type VARCHAR(30) NOT NULL
) PARTITION BY LIST (customer_type);

CREATE TABLE lab3_customers_b2c
    PARTITION OF lab3_customers FOR VALUES IN ('B2C');
CREATE TABLE lab3_customers_b2b
    PARTITION OF lab3_customers FOR VALUES IN ('B2B');
CREATE TABLE lab3_customers_enterprise
    PARTITION OF lab3_customers FOR VALUES IN ('Enterprise');

INSERT INTO lab3_customers VALUES
(1, 'Ann', 'B2C'),
(2, 'Bob', 'B2B'),
(3, 'Corp', 'Enterprise');

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab3_customers WHERE customer_type = 'B2B';

-- Ожидаемая ошибка без DEFAULT (выполните отдельно):
-- INSERT INTO lab3_customers VALUES (100, 'Test User', 'VIP');

CREATE TABLE lab3_customers_default PARTITION OF lab3_customers DEFAULT;
INSERT INTO lab3_customers VALUES (100, 'Test User', 'VIP');

SELECT tableoid::regclass AS partition_name, *
FROM lab3_customers
WHERE id = 100;

-- >>> Часть 6. HASH
DROP TABLE IF EXISTS lab3_user_events CASCADE;

CREATE TABLE lab3_user_events (
    id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    event_type VARCHAR(50),
    created_at TIMESTAMP NOT NULL
) PARTITION BY HASH (user_id);

CREATE TABLE lab3_user_events_0 PARTITION OF lab3_user_events
    FOR VALUES WITH (MODULUS 4, REMAINDER 0);
CREATE TABLE lab3_user_events_1 PARTITION OF lab3_user_events
    FOR VALUES WITH (MODULUS 4, REMAINDER 1);
CREATE TABLE lab3_user_events_2 PARTITION OF lab3_user_events
    FOR VALUES WITH (MODULUS 4, REMAINDER 2);
CREATE TABLE lab3_user_events_3 PARTITION OF lab3_user_events
    FOR VALUES WITH (MODULUS 4, REMAINDER 3);

INSERT INTO lab3_user_events
SELECT g, (random() * 10000)::bigint, 'x', NOW()
FROM generate_series(1, 100000) g;

SELECT tableoid::regclass AS partition_name, COUNT(*)
FROM lab3_user_events
GROUP BY tableoid
ORDER BY partition_name;

-- >>> Часть 8–9. Индексы + partitioning
CREATE INDEX idx_lab3_events_user_id ON lab3_events (user_id);

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab3_events
WHERE created_at >= '2026-09-10'
  AND created_at < '2026-09-11'
  AND user_id = 11;

CREATE INDEX idx_lab3_events_event_type ON lab3_events (event_type);

EXPLAIN (ANALYZE, BUFFERS)
SELECT COUNT(*) FROM lab3_events WHERE event_type = 'click';

-- >>> Часть 10–11. Автосоздание партиций readings (сервис)
-- Список партиций
SELECT c.relname AS partition_name
FROM pg_inherits i
JOIN pg_class c ON c.oid = i.inhrelid
JOIN pg_class p ON p.oid = i.inhparent
WHERE p.relname = 'readings'
ORDER BY 1;

-- Pruning по месяцу
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM readings
WHERE created_at >= date_trunc('month', NOW())
  AND created_at < date_trunc('month', NOW()) + INTERVAL '1 month'
LIMIT 50;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM readings WHERE status = 'NEW' LIMIT 50;

EXPLAIN (ANALYZE, BUFFERS)
SELECT status, COUNT(*) FROM readings GROUP BY status;

-- Создание будущей партиции вручную (пример — поправьте год/месяц при необходимости)
-- CREATE TABLE IF NOT EXISTS readings_2027_01
-- PARTITION OF readings
-- FOR VALUES FROM ('2027-01-01') TO ('2027-02-01');

-- >>> Demo alert: удалить пустую будущую партицию, затем создать снова
-- 1) Смотрим пустые:
SELECT c.relname,
       (xpath('/row/c/text()', query_to_xml(
           format('SELECT COUNT(*) AS c FROM %I', c.relname), false, true, '')))[1]::text::int AS cnt
FROM pg_inherits i
JOIN pg_class c ON c.oid = i.inhrelid
JOIN pg_class p ON p.oid = i.inhparent
WHERE p.relname = 'readings'
ORDER BY 1;

-- 2) Пример CRITICAL-сценария (раскомментируйте ОДНУ пустую партицию):
-- DROP TABLE readings_2026_12;
-- 3) Восстановление:
-- CREATE TABLE readings_2026_12 PARTITION OF readings
-- FOR VALUES FROM ('2026-12-01') TO ('2027-01-01');
