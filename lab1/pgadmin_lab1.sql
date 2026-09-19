-- LAB 1 — Индексы и EXPLAIN ANALYZE (выполнять в pgAdmin Query Tool)

-- >>> Блок 1. Создание таблицы
DROP TABLE IF EXISTS lab1_orders CASCADE;

CREATE TABLE lab1_orders (
    id BIGSERIAL PRIMARY KEY,
    user_id BIGINT NOT NULL,
    product_id BIGINT NOT NULL,
    status VARCHAR(20) NOT NULL,
    amount NUMERIC(10, 2) NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);

-- >>> Блок 2. Генерация 1 000 000 строк (подождите ~10–30 сек)
INSERT INTO lab1_orders (user_id, product_id, status, amount, created_at, updated_at)
SELECT
    (random() * 100000)::BIGINT,
    (random() * 10000)::BIGINT,
    (ARRAY['NEW', 'PAID', 'DELIVERED', 'CANCELLED'])[floor(random() * 4 + 1)],
    random() * 10000,
    NOW() - (random() * INTERVAL '2 years'),
    NOW()
FROM generate_series(1, 1000000);

ANALYZE lab1_orders;

SELECT COUNT(*) AS rows_count FROM lab1_orders;

-- >>> Блок 3. EXPLAIN (без выполнения)
EXPLAIN
SELECT * FROM lab1_orders WHERE user_id = 123;

-- >>> Блок 4. EXPLAIN ANALYZE (с выполнением)
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE user_id = 123;

-- >>> Блок 5. Sequential Scan
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE amount > 0;

-- >>> Блок 6. Первый индекс + повтор измерения
CREATE INDEX idx_lab1_orders_user_id ON lab1_orders(user_id);
ANALYZE lab1_orders;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE user_id = 123;

-- >>> Блок 7–8. Индекс по status и селективность
CREATE INDEX idx_lab1_orders_status ON lab1_orders(status);
ANALYZE lab1_orders;

SELECT status, COUNT(*) FROM lab1_orders GROUP BY status ORDER BY 1;

EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM lab1_orders WHERE status = 'PAID';
EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM lab1_orders WHERE status = 'NEW';
EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM lab1_orders WHERE status = 'DELIVERED';
EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM lab1_orders WHERE status = 'CANCELLED';

-- >>> Блок 9. Range по created_at
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE created_at > NOW() - INTERVAL '7 days';

CREATE INDEX idx_lab1_orders_created_at ON lab1_orders(created_at);
ANALYZE lab1_orders;

EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM lab1_orders WHERE created_at > NOW() - INTERVAL '1 day';
EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM lab1_orders WHERE created_at > NOW() - INTERVAL '7 days';
EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM lab1_orders WHERE created_at > NOW() - INTERVAL '1 month';
EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM lab1_orders WHERE created_at > NOW() - INTERVAL '1 year';

-- >>> Блок 10. Bitmap Scan
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE amount BETWEEN 1000 AND 3000;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE status = 'NEW';

-- >>> Блок 11. Несколько индексов (возможны BitmapAnd)
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE user_id = 123 AND status = 'PAID';

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE user_id < 500 AND status = 'PAID';

-- >>> Блок 12. Составной индекс
CREATE INDEX idx_lab1_orders_user_status ON lab1_orders(user_id, status);
ANALYZE lab1_orders;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE user_id = 123 AND status = 'PAID';

-- >>> Блок 13. Порядок колонок
CREATE INDEX idx_lab1_orders_user_created_at ON lab1_orders(user_id, created_at);
CREATE INDEX idx_lab1_orders_created_at_user ON lab1_orders(created_at, user_id);
ANALYZE lab1_orders;

EXPLAIN (ANALYZE, BUFFERS) SELECT * FROM lab1_orders WHERE user_id = 123;
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE user_id = 123 AND created_at > NOW() - INTERVAL '30 days';
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE created_at > NOW() - INTERVAL '30 days';

-- >>> Блок 14–15. ORDER BY + pagination
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE user_id = 123 ORDER BY created_at DESC;

CREATE INDEX idx_lab1_orders_user_created_at_desc ON lab1_orders(user_id, created_at DESC);
ANALYZE lab1_orders;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE user_id = 123 ORDER BY created_at DESC LIMIT 20;

-- >>> Блок 16. Index Only Scan (+ INCLUDE)
EXPLAIN (ANALYZE, BUFFERS)
SELECT id, user_id FROM lab1_orders WHERE user_id = 123;

CREATE INDEX idx_lab1_orders_user_include ON lab1_orders(user_id) INCLUDE (id, status, created_at);
VACUUM ANALYZE lab1_orders;

EXPLAIN (ANALYZE, BUFFERS)
SELECT id, user_id FROM lab1_orders WHERE user_id = 123;

-- >>> Блок 17. Partial index
EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE status = 'NEW' ORDER BY created_at;

CREATE INDEX idx_lab1_orders_new ON lab1_orders(created_at) WHERE status = 'NEW';
ANALYZE lab1_orders;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_orders WHERE status = 'NEW' ORDER BY created_at;

-- >>> Блок 18. Expression index
DROP TABLE IF EXISTS lab1_users CASCADE;
CREATE TABLE lab1_users (
    id BIGSERIAL PRIMARY KEY,
    email VARCHAR(255) NOT NULL
);

INSERT INTO lab1_users(email)
SELECT 'user' || g || '@example.com' FROM generate_series(1, 200000) g;
INSERT INTO lab1_users(email) VALUES ('test@example.com');
CREATE INDEX idx_lab1_users_email ON lab1_users(email);
ANALYZE lab1_users;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_users WHERE LOWER(email) = 'test@example.com';

CREATE INDEX idx_lab1_users_lower_email ON lab1_users(LOWER(email));
ANALYZE lab1_users;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM lab1_users WHERE LOWER(email) = 'test@example.com';

-- >>> Блок 19. Цена индексов на INSERT
DROP TABLE IF EXISTS lab1_insert_test;
CREATE TABLE lab1_insert_test(id BIGSERIAL PRIMARY KEY, a INT, b INT, c TEXT);

-- засеките время в Messages / вручную
INSERT INTO lab1_insert_test(a, b, c)
SELECT g, g % 100, 'x' FROM generate_series(1, 200000) g;

CREATE INDEX ON lab1_insert_test(a);
CREATE INDEX ON lab1_insert_test(b);
CREATE INDEX ON lab1_insert_test(c);
TRUNCATE lab1_insert_test;

INSERT INTO lab1_insert_test(a, b, c)
SELECT g, g % 100, 'x' FROM generate_series(1, 200000) g;

-- >>> Блок 20. Статистика индексов
SELECT schemaname, relname, indexrelname, idx_scan
FROM pg_stat_user_indexes
WHERE relname LIKE 'lab1_%'
ORDER BY idx_scan;

-- >>> Блок 21. Финальная оптимизация
EXPLAIN (ANALYZE, BUFFERS)
SELECT id, amount, status, created_at
FROM lab1_orders
WHERE user_id = 123
  AND status = 'PAID'
  AND created_at >= NOW() - INTERVAL '30 days'
ORDER BY created_at DESC
LIMIT 50;

CREATE INDEX idx_lab1_final ON lab1_orders(user_id, status, created_at DESC);
ANALYZE lab1_orders;

EXPLAIN (ANALYZE, BUFFERS)
SELECT id, amount, status, created_at
FROM lab1_orders
WHERE user_id = 123
  AND status = 'PAID'
  AND created_at >= NOW() - INTERVAL '30 days'
ORDER BY created_at DESC
LIMIT 50;

-- >>> Блок 22–29. Свой сервис (таблица readings)
-- Убедитесь, что миграции применены (docker compose / liquibase).
SELECT COUNT(*) AS readings_now FROM readings;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM readings WHERE user_id = 1;

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM readings WHERE user_id = 1 AND status = 'COMPLETED';

EXPLAIN (ANALYZE, BUFFERS)
SELECT * FROM readings
WHERE user_id = 1
ORDER BY created_at DESC
LIMIT 20;

SELECT schemaname, relname, indexrelname, idx_scan
FROM pg_stat_user_indexes
WHERE relname LIKE 'readings%'
ORDER BY idx_scan;
