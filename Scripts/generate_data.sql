-- Быстрая генерация readings (оптимизированная)
-- Usage: psql ... -v count=100000 -f Scripts/generate_data.sql

\if :{?count}
\else
\set count 100000
\endif

WITH u AS (SELECT array_agg(id) ids FROM users),
     s AS (SELECT array_agg(id) ids FROM spreads)
INSERT INTO readings (user_id, spread_id, question, status, created_at)
SELECT
    u.ids[1 + (floor(random() * array_length(u.ids, 1)))::int],
    s.ids[1 + (floor(random() * array_length(s.ids, 1)))::int],
    'Generated question #' || g,
    (ARRAY['NEW', 'COMPLETED', 'CANCELLED'])[1 + (floor(random() * 3))::int],
    NOW() - (random() * INTERVAL '365 days')
FROM generate_series(1, :count) g, u, s;

ANALYZE readings;

SELECT COUNT(*) AS readings_count FROM readings;
