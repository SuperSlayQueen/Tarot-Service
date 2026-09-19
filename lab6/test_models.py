"""Checks for the in-memory models; these are NOT database/API tests."""
import sys
import unittest
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'lab5'))
sys.path.insert(0, str(ROOT / 'lab6'))
from sharding_experiment import HashRing, READINGS, hash32, migrated, shard_mod
from shard_query_experiment import ROWS


class ModelTests(unittest.TestCase):
    def test_known_hash(self):
        self.assertEqual(hash32(42), 2714814184)
        self.assertEqual(shard_mod(42, 3), 1)

    def test_invalid_ring(self):
        for shards, count in (([], 100), ([0, 0], 100), ([0], 0)):
            with self.assertRaises(ValueError):
                HashRing(shards, count)

    def test_user_distribution(self):
        self.assertEqual(Counter(shard_mod(u, 3) for _, u in READINGS),
                         {0: 33540, 1: 33500, 2: 32960})
        self.assertEqual(len(READINGS), len(ROWS))
        self.assertTrue(all((r['id'], r['user_id']) == pair
                            for r, pair in zip(ROWS, READINGS)))

    def test_movement_uses_user_key(self):
        keys = [u for _, u in READINGS]
        self.assertEqual(migrated(keys, lambda u: shard_mod(u, 3),
                                  lambda u: shard_mod(u, 4)), (75940, 100000))
        before, after = HashRing(range(3)), HashRing(range(4))
        self.assertEqual(migrated(keys, before.shard, after.shard), (27880, 100000))
        for u in range(1, 5001):
            if before.shard(u) != after.shard(u):
                self.assertEqual(after.shard(u), 3)
        self.assertEqual(before.ring, HashRing([2, 0, 1]).ring)

    def test_ring_boundaries(self):
        from unittest.mock import patch
        ring = HashRing(range(3))
        for point, owner in ring.ring:
            with patch('sharding_experiment.hash64', return_value=point):
                self.assertEqual(ring.shard(42), owner)
        with patch('sharding_experiment.hash64', return_value=ring.points[-1] + 1):
            self.assertEqual(ring.shard(42), ring.ring[0][1])

    def test_aggregation(self):
        parts = [Counter(r['status'] for r in ROWS if r['shard'] == s) for s in range(3)]
        total = sum(parts, Counter())
        self.assertEqual(total, Counter(r['status'] for r in ROWS))
        self.assertEqual(total, {'NEW': 33333, 'COMPLETED': 33334, 'CANCELLED': 33333})

    def test_top_k_and_pagination(self):
        order = lambda r: (r['created_offset_min'], r['id'])
        for offset, size in ((0, 20), (40, 20), (1000, 17)):
            pool = []
            for s in range(3):
                pool.extend(sorted((r for r in ROWS if r['shard'] == s), key=order)[:offset + size])
            merged = sorted(pool, key=order)[offset:offset + size]
            expected = sorted(ROWS, key=order)[offset:offset + size]
            self.assertEqual(merged, expected)

    def test_unavailable_data(self):
        rows = [r for r in ROWS if r['shard'] == 2]
        self.assertEqual(len(rows), 32960)
        self.assertEqual(len({r['user_id'] for r in rows}), 1648)
        self.assertEqual(sum(r['cards'] for r in rows), 65900)


if __name__ == '__main__':
    unittest.main(verbosity=2)
