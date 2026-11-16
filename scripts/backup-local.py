#!/usr/bin/env python3
"""Back up PostgreSQL and private uploads, then restore into a NEW database.
Stop writes to the source API first for a consistent database/files snapshot.
Never drops, truncates, or replaces an existing database.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tarfile

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--config', type=Path, default=Path.home()/'.config/serviceops/demo-environment.json')
parser.add_argument('--output', type=Path, default=Path.home()/'.local/share/serviceops/backups')
args = parser.parse_args()
config = json.loads(args.config.read_text())
fields = dict((k.strip().lower(), v.strip()) for k, v in
              (p.split('=',1) for p in config['ConnectionStrings__DefaultConnection'].split(';') if '=' in p))
env = os.environ.copy()
env.update(PGHOST=fields.get('host','localhost'), PGPORT=fields.get('port','5432'),
           PGUSER=fields.get('username','postgres'), PGPASSWORD=fields.get('password',''), PGDATABASE=fields['database'])
storage = Path(config['Attachments__Root']).expanduser().resolve()
stamp = datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%d%H%M%S%f')
folder = args.output.expanduser().resolve()/stamp
folder.mkdir(parents=True, mode=0o700)
os.chmod(folder, 0o700)

def run(command, database=None, sql=None):
    variables = env.copy()
    if database:
        variables['PGDATABASE'] = database
    result = subprocess.run(command, input=sql, text=True, capture_output=True, env=variables)
    if result.returncode:
        raise RuntimeError(command[0]+' failed; private diagnostics withheld. No source data was removed.')
    return result.stdout.strip()

def query(sql, database=None):
    return run(['psql','-X','-qAt','-v','ON_ERROR_STOP=1'], database, sql)

def counts(database=None):
    tables = json.loads(query("SELECT json_agg(tablename ORDER BY tablename) FROM pg_tables WHERE schemaname='public';", database))
    return {table:int(query('SELECT count(*) FROM "'+table.replace('"','""')+'";', database)) for table in tables}

def hashes(root):
    return {str(file.relative_to(root)):hashlib.sha256(file.read_bytes()).hexdigest()
            for file in root.rglob('*') if file.is_file()} if root.exists() else {}

source_counts = counts()
source_hashes = hashes(storage)
dump = folder/'postgres.dump'
run(['pg_dump','--format=custom','--no-owner','--no-privileges','--file',str(dump)])
os.chmod(dump, 0o600)
archive = folder/'attachments.tar.gz'
with tarfile.open(archive,'w:gz') as stream:
    if storage.exists():
        if any(file.is_symlink() for file in storage.rglob('*')):
            raise RuntimeError('Symlink in upload storage requires review; source files preserved.')
        stream.add(storage, arcname='attachments')
os.chmod(archive, 0o600)
restore = 'serviceops_restore_verify_'+stamp
query('CREATE DATABASE "'+restore+'";', 'postgres')
run(['pg_restore','--exit-on-error','--no-owner','--no-privileges','--dbname',restore,str(dump)], restore)
restored_counts = counts(restore)
extracted = folder/'restore-files'
extracted.mkdir(mode=0o700)
with tarfile.open(archive) as stream:
    for member in stream.getmembers():
        destination = (extracted/member.name).resolve()
        if extracted not in destination.parents or not (member.isfile() or member.isdir()):
            raise RuntimeError('Unsafe archive member; automatic extraction refused.')
    stream.extractall(extracted)
restored_hashes = hashes(extracted/'attachments')
report = {'sourceDatabase':fields['database'], 'restoreDatabase':restore,
          'sourceCounts':source_counts, 'restoredCounts':restored_counts,
          'databaseRestoreCountsMatch':source_counts == restored_counts,
          'attachmentFileCount':len(source_hashes), 'attachmentRestoreHashesMatch':source_hashes == restored_hashes,
          'sourceWritesPerformed':False, 'backupDirectory':str(folder),
          'backupHashes':{file.name:hashlib.sha256(file.read_bytes()).hexdigest() for file in [dump,archive]}}
(folder/'verification.json').write_text(json.dumps(report,indent=2)+'\n')
os.chmod(folder/'verification.json',0o600)
print('Backup stored privately at '+str(folder))
print('Restore database: '+restore)
if not report['databaseRestoreCountsMatch'] or not report['attachmentRestoreHashesMatch']:
    raise SystemExit('Restore mismatch: preserve backup, stop writes and investigate before proceeding.')
print('Database counts and attachment hashes match. Source data was preserved.')
