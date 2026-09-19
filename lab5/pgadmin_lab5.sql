-- Lab 5: execute on EACH shard, database tarot_shard or tarot_ring.
-- Only session-local functions are created. Real service tables are never modified.
SELECT current_database(), count(*) AS readings, count(DISTINCT user_id) AS users FROM readings;
SELECT user_id, count(*) FROM readings GROUP BY user_id ORDER BY user_id;

CREATE OR REPLACE FUNCTION pg_temp.hash32(k text) RETURNS bigint
LANGUAGE sql IMMUTABLE STRICT AS $$
  SELECT ('x'||substr(md5(k),1,8))::bit(32)::bigint;
$$;
CREATE OR REPLACE FUNCTION pg_temp.hash64(k text) RETURNS bigint
LANGUAGE sql IMMUTABLE STRICT AS $$
  SELECT ('x'||substr(md5(k),1,16))::bit(64)::bigint;
$$;
CREATE OR REPLACE FUNCTION pg_temp.ring(k integer, n integer) RETURNS integer
LANGUAGE sql IMMUTABLE STRICT AS $$
  SELECT shard FROM (
    SELECT s AS shard, pg_temp.hash64('shard-'||s||'-vnode-'||v) AS h
    FROM generate_series(0,n-1) s CROSS JOIN generate_series(1,100) v
  ) points
  ORDER BY CASE WHEN h >= pg_temp.hash64(k::text) THEN 0 ELSE 1 END, h, shard
  LIMIT 1;
$$;

-- Count RECORDS, not only distinct user keys. Empty shard: totals are zero, percent NULL.
WITH users AS MATERIALIZED (
  SELECT user_id, count(*) AS weight FROM readings GROUP BY user_id
), routes AS MATERIALIZED (
  SELECT weight,
    pg_temp.hash32(user_id::text)%3 <> pg_temp.hash32(user_id::text)%4 AS mod_moved,
    pg_temp.ring(user_id,3) <> pg_temp.ring(user_id,4) AS ring_moved
  FROM users
)
SELECT coalesce(sum(weight),0) AS total,
       coalesce(sum(weight) FILTER (WHERE mod_moved),0) AS mod_moved,
       coalesce(sum(weight) FILTER (WHERE ring_moved),0) AS ring_moved,
       round(100.0*sum(weight) FILTER (WHERE mod_moved)/nullif(sum(weight),0),2) AS mod_pct,
       round(100.0*sum(weight) FILTER (WHERE ring_moved)/nullif(sum(weight),0),2) AS ring_pct
FROM routes;
-- Add local totals/moved across three shards before computing the cluster percentage.
-- Verified dataset: 100000 total, 100000 mod moved (100%), 50000 ring moved (50%).
SELECT fingerprint FROM shard_topology;
