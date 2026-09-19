-- Lab 6: real service tables; run on each shard, no modifications.
-- Modulo: user 1 is on shard1; consistent: user 1 is on shard2.
EXPLAIN (ANALYZE, BUFFERS)
SELECT id,user_id,created_at FROM readings WHERE user_id=1
ORDER BY created_at DESC,id DESC LIMIT 20;

SELECT status,count(*) FROM readings GROUP BY status ORDER BY status;
SELECT count(*) AS readings, count(DISTINCT user_id) AS users FROM readings;

-- Local parent/child JOIN. Reference names live in Primary and are joined by backend.
SELECT r.spread_id,count(*) AS readings,
       sum(coalesce(c.cnt,0)) AS cards
FROM readings r
LEFT JOIN (SELECT reading_id,count(*) AS cnt FROM reading_cards GROUP BY reading_id) c
ON c.reading_id=r.id GROUP BY r.spread_id ORDER BY r.spread_id;

SELECT id,user_id,created_at FROM readings ORDER BY created_at DESC,id DESC LIMIT 20;
-- Compare to GET http://localhost:5085/api/reading?pageSize=20 .
-- Backend takes top(page*pageSize) from each shard, merges, then applies global offset.

SELECT count(*) AS orphan_cards FROM reading_cards rc
LEFT JOIN readings r ON r.id=rc.reading_id WHERE r.id IS NULL;
-- Expected: 0. Actual FK also prevents orphan cards.

-- Failure demo in terminal, NOT SQL:
-- docker stop lab5_shard2
-- modulo user1 -> HTTP 200; user2 -> HTTP 503; global statistics -> HTTP 503.
-- docker start lab5_shard2
-- Wait until pg_isready succeeds; global count returns 100000 again.
-- Do not remove a temporarily failed shard from the routing configuration.
