#!/usr/bin/env python3
import os,pathlib,sys,time
pid=pathlib.Path(os.environ['IH_LEGACY_TEST_PID']);pid.write_text(str(os.getpid()))
source=pathlib.Path(sys.argv[-1])
if source.stem=='success':
 directory=pathlib.Path(sys.argv[sys.argv.index('--outdir')+1]);(directory/(source.stem+'.pptx')).write_bytes(b'PK\x03\x04authored-wrapper-fixture')
elif source.stem=='failure':sys.exit(1)
else:time.sleep(30)
