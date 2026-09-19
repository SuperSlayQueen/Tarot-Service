"""Copy the current two-user lab dataset to separate consistent-hash databases.
Never drops or overwrites databases. Stop the lab API and writers before running.
Requires Docker CLI, initialized tarot_shard databases and Python 3.
"""
import subprocess
from sharding_experiment import HashRing


def docker(*args, data=None):
    return subprocess.run(['docker', *args], input=data, capture_output=True, check=True).stdout


def sql(shard, database, statement):
    return docker('exec', '-i', f'lab5_shard{shard}', 'psql', '-X', '-v',
                  'ON_ERROR_STOP=1', '-U', 'tarot', '-d', database, '-At',
                  data=statement.encode('utf-8')).decode('utf-8').strip()


def main():
    # This fixture contains users 1 and 2 only. Refuse a different dataset rather
    # than silently misroute it. This is an offline copy, not a production migration.
    destinations = {0: 0, 1: 2, 2: 1}
    ring = HashRing(range(3), 100)
    for source, target in destinations.items():
        if sql(target, 'postgres', "SELECT 1 FROM pg_database WHERE datname='tarot_ring';"):
            raise RuntimeError('tarot_ring already exists; no data was changed')
        users = sql(source, 'tarot_shard', 'SELECT DISTINCT user_id FROM readings ORDER BY user_id;')
        for user in users.splitlines():
            if ring.shard(int(user)) != target:
                raise RuntimeError('Dataset differs from the verified two-user fixture')
    for source, target in destinations.items():
        sql(target, 'postgres', 'CREATE DATABASE tarot_ring OWNER tarot;')
        dump = docker('exec', f'lab5_shard{source}', 'pg_dump', '-U', 'tarot',
                      '-d', 'tarot_shard', '--no-owner', '--no-privileges')
        docker('exec', '-i', f'lab5_shard{target}', 'psql', '-X', '-v',
               'ON_ERROR_STOP=1', '-U', 'tarot', '-d', 'tarot_ring', data=dump)
        sql(target, 'tarot_ring', "UPDATE shard_topology SET fingerprint='md5-v1:consistent:100:0,1,2';")
        # Compare all actual data columns, not only counts. pg_dump also preserves sequences.
        for table in ('readings', 'reading_cards'):
            query = f'COPY (SELECT * FROM {table} ORDER BY id) TO STDOUT WITH CSV;'
            if sql(source, 'tarot_shard', query) != sql(target, 'tarot_ring', query):
                raise RuntimeError(f'Data mismatch for {table}; source databases remain intact')
        print(f'Source {source} -> ring shard {target}: exact data match; '
              + sql(target, 'tarot_ring', 'SELECT count(*) FROM readings;') + ' readings')


if __name__ == '__main__':
    main()
