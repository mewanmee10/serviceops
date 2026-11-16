#!/usr/bin/env python3
"""Run the existing application with the explicit private serviceops_dev configuration.
Does not create, migrate, seed or reset the database. No fallback database.
"""
import argparse,json,os,subprocess
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('service',choices=['api','frontend']);a=p.parse_args();root=Path(__file__).resolve().parents[1];path=Path.home()/'.config/serviceops/primary-environment.json';cfg=json.loads(path.read_text());fields=dict((k.strip().lower(),v.strip())for k,v in(x.split('=',1)for x in cfg['ConnectionStrings__DefaultConnection'].split(';')if '='in x));assert fields['database']=='serviceops_dev';env=os.environ.copy();env.update({k:str(v)for k,v in cfg.items()});env.update(SeedDemo__Enabled='false',SeedAdmin__Password='',BACKEND_API_URL='http://127.0.0.1:5130');api=root/'backend/ServiceOps.Api';cwd=api if a.service=='api'else root/'frontend';cmd=['dotnet',str(api/'bin/Debug/net10.0/ServiceOps.Api.dll'),'--urls','http://127.0.0.1:5130']if a.service=='api'else['node',str(root/'frontend/node_modules/next/dist/bin/next'),'start','--hostname','localhost','--port','3030'];raise SystemExit(subprocess.call(cmd,cwd=cwd,env=env))
