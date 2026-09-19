#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Lab 6 — что происходит с запросами сервиса после шардирования.

Датасет тот же, что в lab5 (100 000 readings, 5000 пользователей), shard key = user_id,
роутер = lab5_shard_mod(user_id, 3). Скрипт показывает в цифрах:
  * single-shard query;
  * агрегацию (scatter-gather + merge на координаторе);
  * ORDER BY ... LIMIT (top-N с нескольких шардов);
  * JOIN между шардами (объём данных для передачи);
  * отказ одного шарда (какие данные/endpoint'ы страдают).

Запуск:  python lab6/shard_query_experiment.py
"""

import sys
import os
from collections import Counter, defaultdict

sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'lab5'))
from sharding_experiment import shard_mod, fmt  # noqa: E402

SHARDS = 3
BASE_TIME = "2026-09-01 00:00:00"


def build_rows():
    """Детерминированный датасет (совпадает с lab5/pgadmin_lab5.sql)."""
    rows = []
    for g in range(1, 100_001):
        rows.append({
            'id': g,
            'user_id': 1 + (g % 5000),
            'spread_id': 1 + (g % 12),
            'status': ['NEW', 'COMPLETED', 'CANCELLED'][g % 3],
            'created_offset_min': g % 100_000,      # created_at = BASE - offset
            'cards': 1 + (g % 3),                   # сколько reading_cards у гадания
            'shard': shard_mod(1 + (g % 5000), SHARDS),
        })
    return rows


ROWS = build_rows()


def section_single_shard():
    print("\n## 2. Single-Shard Query — `WHERE user_id = ?`")
    for user in (42, 1337, 7):
        per = Counter(r['shard'] for r in ROWS if r['user_id'] == user)
        print()
        print("- `GET /api/reading?userId=%d` (shard = %d): строки по шардам = %s" %
              (user, shard_mod(user, SHARDS),
               {s: per.get(s, 0) for s in range(SHARDS)}))
    print()
    print("Чтение только с нужного шарда: 1 запрос. Если бы роутинг был по `id` записи"
          " (или ключ отсутствовал) — пришлось бы опросить все %d шарда (read amplification x%d)." %
          (SHARDS, SHARDS))


def section_aggregation():
    print("\n## 3. Агрегирующий запрос — `GET /api/stats/readings-by-status`")
    print()
    print("`SELECT status, COUNT(*) FROM readings GROUP BY status` — считать только на одном шарде нельзя.")
    print()
    print("| Shard | NEW | COMPLETED | CANCELLED | Всего |")
    print("|-------|----:|----------:|----------:|------:|")
    per_shard = defaultdict(Counter)
    for r in ROWS:
        per_shard[r['shard']][r['status']] += 1
    for s in range(SHARDS):
        c = per_shard[s]
        print("| Shard %d | %s | %s | %s | %s |" %
              (s, fmt(c['NEW']), fmt(c['COMPLETED']), fmt(c['CANCELLED']), fmt(sum(c.values()))))
    total = Counter()
    for c in per_shard.values():
        total.update(c)
    print("| **Итого (merge)** | **%s** | **%s** | **%s** | **%s** |" %
          (fmt(total['NEW']), fmt(total['COMPLETED']), fmt(total['CANCELLED']),
           fmt(sum(total.values()))))
    print()
    print("Ошибка, если взять один шард: Shard 0 отдаст NEW=%s вместо %s "
          "(недоучёт %.2f%% данных)." %
          (fmt(per_shard[0]['NEW']), fmt(total['NEW']),
           100.0 - 100.0 * sum(per_shard[0].values()) / sum(total.values())))

def section_order_limit():
    print("\n## 5. `ORDER BY created_at DESC LIMIT 20` (лента гаданий)")
    print()
    print("`GET /api/reading?sort=-created_at&pageSize=20` без `userId` — данные на всех шардах.")
    top = sorted(ROWS, key=lambda r: r['created_offset_min'])[:20]
    true_ids = [r['id'] for r in top]
    print()
    print("- истинный глобальный топ-20 (по `created_at DESC`): id = %s" % true_ids)
    by_id = {r['id']: r for r in ROWS}
    per_shard_top = {}
    for s in range(SHARDS):
        rows = sorted((r for r in ROWS if r['shard'] == s), key=lambda r: r['created_offset_min'])[:20]
        per_shard_top[s] = [r['id'] for r in rows]
        print("- Shard %d top-20: %s" % (s, per_shard_top[s]))
    pool = {rid for s in range(SHARDS) for rid in per_shard_top[s]}
    merged = sorted((by_id[rid] for rid in pool), key=lambda r: r['created_offset_min'])[:20]
    print()
    print("- merge(top-20 каждого шарда) = %s" % [r['id'] for r in merged])
    print("- совпадает с истинным топ-20: %s" % ([r['id'] for r in merged] == true_ids))
    coverage = [len([i for i in per_shard_top[s] if i in true_ids]) for s in range(SHARDS)]
    print("- вклад шардов в истинный топ-20: %s" % coverage)
    print("- если взять топ-20 ТОЛЬКО с Shard 0, получим %d из 20 (%d%% правильных)" %
          (coverage[0], coverage[0] * 5))
    print()
    print("Правило: с каждого шарда берём `LIMIT (offset + limit)`, затем merge и обрезка;")
    print("`OFFSET` применяется ТОЛЬКО после объединения результатов.")


def section_join():
    print("\n## 4. JOIN между шардами — `GET /api/stats/deck-usage`")
    print()
    joined = sum(r['cards'] for r in ROWS)
    print("- `readings`: %s строк (по шардам: %s)" %
          (fmt(len(ROWS)), [fmt(sum(1 for r in ROWS if r['shard'] == s)) for s in range(SHARDS)]))
    print("- `reading_cards`: %s строк (по шардам: %s)" %
          (fmt(joined), [fmt(sum(r['cards'] for r in ROWS if r['shard'] == s)) for s in range(SHARDS)]))
    print("- `cards` / `decks` / `spreads` / `users` — маленькие справочные таблицы "
          "(их можно реплицировать на каждый шард)")
    print()
    print("Запрос `decks -> cards -> reading_cards -> readings` требует данных с **всех** шардов:")
    print("одним SQL к одному PostgreSQL он не выполняется. Координатор обязан либо")
    print("(а) переслать %s строк `reading_cards` + %s строк `readings`," %
          (fmt(joined), fmt(len(ROWS))))
    print("либо (б) получить с каждого шарда частичный агрегат и сложить его.")
    print()
    print("Локально JOIN выполняется только внутри шарда: `readings JOIN reading_cards`")
    print("(reading_cards пишутся на тот же шард, что и их гадание),")
    print("поэтому ключ маршрутизации `reading_cards` должен совпадать с ключом `readings`.")


def section_failure():
    print("\n## 6. Отказ одного shard (Shard 2 недоступен)")
    dead = 2
    jobs = [r for r in ROWS if r['shard'] == dead]
    users = sorted({r['user_id'] for r in jobs})
    print()
    print("- недоступно пользователей: %s из 5 000 (%.2f%%)" %
          (fmt(len(users)), 100.0 * len(users) / 5000))
    print("- недоступно readings: %s из 100 000 (%.2f%%)" %
          (fmt(len(jobs)), 100.0 * len(jobs) / len(ROWS)))
    print("- недоступно reading_cards: %s" % fmt(sum(r['cards'] for r in jobs)))
    print()
    print("Перестают работать (для 1/3 пользователей): `GET /api/reading?userId=`, "
          "`GET /api/users/{id}/readings`, `POST/PUT/DELETE /api/reading`;")
    print("деградируют до частичных результатов: `GET /api/reading` (общая лента), "
          "`GET /api/stats/readings-by-status`, `GET /api/stats/deck-usage`, "
          "`GET /api/stats/spread-stats`.")
    print("Продолжают работать: `GET /api/card*`, `GET /api/deck*`, `GET /api/spread*` "
          "(справочные таблицы реплицированы), `/health`, аутентификация.")


def section_hot():
    print("\n## 7. Hot shard — строки равны, нагрузка нет")
    rows = Counter(r['shard'] for r in ROWS)
    whales = {7: 300_000, 42: 300_000, 1337: 300_000}
    load = Counter({s: rows.get(s, 0) for s in range(SHARDS)})
    for u, extra in whales.items():
        load[shard_mod(u, SHARDS)] += extra
    total_load = sum(load.values())
    print()
    print("| Shard | Строк | Доля строк | Запросов | Доля запросов |")
    print("|-------|------:|-----------:|---------:|--------------:|")
    for s in range(SHARDS):
        print("| Shard %d | %s | %.2f%% | %s | %.2f%% |" %
              (s, fmt(rows[s]), 100.0 * rows[s] / len(ROWS), fmt(load[s]),
               100.0 * load[s] / total_load))
    print()
    print("max/min по строкам = %.3f, по запросам = %.3f" %
          (max(rows.values()) / min(rows.values()), max(load.values()) / min(load.values())))


if __name__ == '__main__':
    print("# Lab 6 — запросы сервиса после шардирования (данные lab5)")
    print()
    print("Датасет: %s readings, %d шарда, shard key = `user_id`, роутер `hash %% 3`." %
          (fmt(len(ROWS)), SHARDS))
    section_single_shard()
    section_aggregation()
    section_join()
    section_order_limit()
    section_failure()
    section_hot()
