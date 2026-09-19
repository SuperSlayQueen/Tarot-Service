#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Lab 5 — эксперимент по шардированию сущности `readings` (Tarot Reading Service).

Сравниваются две стратегии маршрутизации записи на шард:
  A) shard = hash(shard_key) % N          (прямое деление)
  B) Consistent Hashing - hash ring с virtual nodes

Хеш-функции специально совпадают с SQL-реализацией из lab5/pgadmin_lab5.sql:
  hash32(key) = ('x' || substr(md5(key::text), 1, 8))::bit(32)::bigint   -> 0 .. 2^32-1
  hash64(key) = ('x' || substr(md5(key::text), 1, 16))::bit(64)::bigint  -> кольцо

Скрипт печатает отчёт в Markdown, который вставляется в lab5/REPORT.md.
Запуск:  python lab5/sharding_experiment.py
"""

from bisect import bisect_left
import hashlib
from collections import Counter

# ----------------------------- hash-функции -------------------------------- #


def hash32(key):
    """0..2^32-1. Совпадает с ('x'||substr(md5(key::text),1,8))::bit(32)::bigint."""
    return int(hashlib.md5(str(key).encode()).hexdigest()[:8], 16)


def hash64(key):
    """Знаковое 64-битное значение (кольцо).

    Совпадает с ('x'||substr(md5(key::text),1,16))::bit(64)::bigint в PostgreSQL:
    старший бит трактуется как знак, поэтому порядок точек кольца в SQL и здесь
    совпадает побитово.
    """
    h = int(hashlib.md5(str(key).encode()).hexdigest()[:16], 16)
    return h - (1 << 64) if h >= (1 << 63) else h


def shard_mod(key, n):
    """Стратегия A: shard = hash(key) % N."""
    return hash32(key) % n


class HashRing:
    """Стратегия B: Consistent Hash Ring (virtual nodes = vnode на шард)."""

    def __init__(self, shards, vnodes=100):
        keys = list(shards)
        if not keys or len(keys) != len(set(keys)):
            raise ValueError("Shards must be nonempty and unique")
        if vnodes < 1:
            raise ValueError("vnodes must be positive")
        self.vnodes = vnodes
        self.ring = []
        for s in keys:
            for i in range(1, vnodes + 1):
                # имя vnode зависит ТОЛЬКО от индекса шарда -> добавление shard-3
                # не меняет позиции vnode-точек шардов 0..2 (стабильность кольца)
                self.ring.append((hash64("shard-%d-vnode-%d" % (s, i)), s))
        self.ring.sort()
        self.points = [p[0] for p in self.ring]

    def shard(self, key):
        k = hash64(key)
        i = bisect_left(self.points, k)
        if i == len(self.points):
            i = 0  # замыкание кольца
        return self.ring[i][1]


# ------------------------------- данные ------------------------------------ #

TOTAL_READINGS = 100_000
UNIQUE_USERS = 5_000

# readings: id = 1..100000, user_id равномерно по 5000 пользователям
READINGS = [(i, 1 + (i % UNIQUE_USERS)) for i in range(1, TOTAL_READINGS + 1)]


def fmt(n):
    return format(n, ",").replace(",", " ")


def show_dist(title, counter, n_shards):
    total = sum(counter.values()) or 1
    print("\n### %s" % title)
    print()
    print("| Shard | Записей | Доля |")
    print("|-------|--------:|-----:|")
    for s in range(n_shards):
        cnt = counter.get(s, 0)
        print("| Shard %d | %s | %.2f%% |" % (s, fmt(cnt), 100.0 * cnt / total))
    print("| **Итого** | **%s** | **100.00%%** |" % fmt(total))
    vals = [counter.get(s, 0) for s in range(n_shards)]
    print()
    print("max/min = %.4f (идеал 1.0), stddev = %.1f" %
          (max(vals) / min(vals),
           (sum((v - total / n_shards) ** 2 for v in vals) / n_shards) ** 0.5))


def migrated(keys, router_before, router_after):
    moved = sum(1 for k in keys if router_before(k) != router_after(k))
    return moved, len(keys)


def main():
    print("# Lab 5 — результаты эксперимента (Python 3.10, md5-хеш)")
    print()
    print("Датасет: %s readings, %s уникальных user_id." %
          (fmt(TOTAL_READINGS), fmt(UNIQUE_USERS)))

    # --- 1. Распределение: hash(key) % N ---------------------------------- #
    print("\n## Часть 4. Распределение данных, стратегия hash(key) % N")
    shard_by_id = Counter(shard_mod(rid, 3) for rid, _ in READINGS)
    show_dist("100 000 записей, shard key = readings.id -> 3 шарда", shard_by_id, 3)

    shard_by_user = Counter(shard_mod(uid, 3) for _, uid in READINGS)
    show_dist("100 000 записей, shard key = readings.user_id -> 3 шарда", shard_by_user, 3)

    # --- 2. 3 -> 4 шарда: hash(key) % N ----------------------------------- #
    print("\n## Часть 5. 3 шарда -> 4 шарда (hash % N)")
    keys = [uid for _, uid in READINGS]
    moved, total = migrated(keys, lambda k: shard_mod(k, 3), lambda k: shard_mod(k, 4))
    print()
    print("| Метрика | Значение |")
    print("|---------|---------:|")
    print("| Всего записей | %s |" % fmt(total))
    print("| Изменили шард | %s |" % fmt(moved))
    print("| Не изменили | %s |" % fmt(total - moved))
    print("| **Процент перемещаемых** | **%.2f%%** |" % (100.0 * moved / total))
    print("| Теоретическое ожидание | 75.00% (совпало 3 из 12 классов r mod 12) |")

    # --- 3. 3 -> 4 шарда: Consistent Hashing ------------------------------ #
    print("\n## Часть 7. 3 шарда -> 4 шарда (Consistent Hashing)")
    for vnodes in (1, 10, 100, 200):
        r3 = HashRing([0, 1, 2], vnodes)
        r4 = HashRing([0, 1, 2, 3], vnodes)
        moved, total = migrated(keys, r3.shard, r4.shard)
        print()
        print("vnodes = %d: перемещено %s из %s = **%.2f%%**" %
              (vnodes, fmt(moved), fmt(total), 100.0 * moved / total))

    # --- 4. Качество кольца: распределение по 3 шардам --------------------- #
    print("\n### Consistent Hashing: распределение по 3 шардам")
    print()
    for vnodes in (1, 10, 100):
        ring = HashRing([0, 1, 2], vnodes)
        c = Counter(ring.shard(uid) for _, uid in READINGS)
        vals = [c.get(s, 0) for s in range(3)]
        print("- vnodes = %d: %s (max/min = %.3f)" %
              (vnodes, vals, max(vals) / min(vals)))

    # --- 5. Удаление шарда 4 -> 3 ----------------------------------------- #
    print("\n## 4 шарда -> 3 шарда (удалён Shard 3)")
    moved, total = migrated(keys, lambda k: shard_mod(k, 4), lambda k: shard_mod(k, 3))
    print()
    print("- hash %% N: перемещено %.2f%% записей" % (100.0 * moved / total))
    r4 = HashRing([0, 1, 2, 3], 100)
    r3 = HashRing([0, 1, 2], 100)
    moved, total = migrated(keys, r4.shard, r3.shard)
    print("- Consistent Hashing: перемещено %.2f%% записей "
          "(только данные удалённого шарда)" % (100.0 * moved / total))

    # --- 6. Hot shard: строки равны, нагрузка - нет ------------------------- #
    print("\n## Hot shard: равные строки != равная нагрузка")
    rows = Counter(shard_mod(uid, 3) for _, uid in READINGS)
    show_dist("Строки readings на шард (shard key = user_id, hash % 3)", rows, 3)

    # Запросы: 1 чтение ленты на каждое гадание + "киты" (feed polling / dashboard)
    whales = {7: 300_000, 42: 300_000, 1337: 300_000}
    load = Counter()
    for _, uid in READINGS:
        load[shard_mod(uid, 3)] += 1
    for uid, extra in whales.items():
        load[shard_mod(uid, 3)] += extra

    total_load = sum(load.values())
    print()
    print("### Запросы (GET /api/reading?userId=) на шард")
    print()
    print("| Shard | Строк readings | Доля строк | Запросов | Доля запросов | «Кит» на шарде |")
    print("|-------|---------------:|-----------:|---------:|--------------:|----------------|")
    for sid in range(3):
        whale_users = sorted(u for u in whales if shard_mod(u, 3) == sid)
        print("| Shard %d | %s | %.2f%% | %s | %.2f%% | %s |" %
              (sid, fmt(rows.get(sid, 0)), 100.0 * rows.get(sid, 0) / sum(rows.values()),
               fmt(load.get(sid, 0)), 100.0 * load.get(sid, 0) / total_load,
               ", ".join("user_id=%d" % u for u in whale_users) or "-"))
    print()
    print("Вывод: строки разложены почти идеально (max/min = %.3f), "
          "но нагрузка — max/min = %.3f." %
          (max(rows.values()) / min(rows.values()), max(load.values()) / min(load.values())))


if __name__ == "__main__":
    main()
