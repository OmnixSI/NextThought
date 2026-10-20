#!/usr/bin/env python3
import sys,json,threading,os,time
if sys.argv[1:3]==['login','status']:
 print('Logged in using ChatGPT');sys.exit(0)
if sys.argv[1:2]==['--version']:
 print('codex-cli test');sys.exit(0)
if sys.argv[1:2]==['exec']:
 sys.stdin.read()
 print(json.dumps({'type':'item.completed','item':{'type':'agent_message','text':'done'}}),flush=True)
 sys.exit(0)
lock=threading.Lock()
def emit(obj):
 with lock:
  print(json.dumps(obj),flush=True)
def reply(i,result):emit({'id':i,'result':result})
def later(t,fn):
 timer=threading.Timer(t,fn);timer.daemon=True;timer.start()
for line in sys.stdin:
 msg=json.loads(line);i=msg.get('id');method=msg.get('method');p=msg.get('params',{})
 if method=='initialize':reply(i,{'userAgent':'fake'})
 elif method=='account/read':reply(i,{'requiresOpenaiAuth':False})
 elif method=='thread/start':
  if p.get('model')=='slow-thread':later(2,lambda i=i:reply(i,{'thread':{'id':'slow'}}))
  else:reply(i,{'thread':{'id':str(i)}})
 elif method=='turn/start':
  thread=p['threadId'];turn=str(i)
  later(.2,lambda i=i,turn=turn:reply(i,{'turn':{'id':turn}}))
  def finish(thread=thread,turn=turn):
   emit({'method':'item/completed','params':{'threadId':thread,'turnId':turn,'item':{'id':turn,'type':'agentMessage','text':'done','phase':'final_answer'}}})
   emit({'method':'turn/completed','params':{'threadId':thread,'turn':{'id':turn,'status':'completed'}}})
  later(.4,finish)
 elif method=='turn/interrupt':
  with open(os.environ['PROBE_INTERRUPT_LOG'],'a') as f:f.write(p['turnId']+'\n')
  reply(i,{})
