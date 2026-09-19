"""out/<lang>/1..5.png → Play phoneScreenshots 교체 (deleteall + upload). --commit 없으면 validate 후 폐기."""
import sys, os, glob
from googleapiclient.http import MediaFileUpload
from play import svc
PKG='com.animalpop.app'; ROOT=os.path.dirname(os.path.abspath(__file__)); COMMIT='--commit' in sys.argv
langs=sorted(os.path.basename(d) for d in glob.glob(f"{ROOT}/out/*") if os.path.basename(d)!='test')
e=svc.edits().insert(packageName=PKG,body={}).execute(); eid=e['id']
try:
    have={l['language'] for l in svc.edits().listings().list(packageName=PKG,editId=eid).execute()['listings']}
    for lg in langs:
        assert lg in have,(lg,'no listing')
        fs=[f"{ROOT}/out/{lg}/{i}.png" for i in range(1,6)]; assert all(os.path.exists(f) for f in fs),lg
        svc.edits().images().deleteall(packageName=PKG,editId=eid,language=lg,imageType='phoneScreenshots').execute()
        for f in fs:
            svc.edits().images().upload(packageName=PKG,editId=eid,language=lg,imageType='phoneScreenshots',media_body=MediaFileUpload(f,mimetype='image/png')).execute()
        print(lg,'uploaded 5',flush=True)
    svc.edits().validate(packageName=PKG,editId=eid).execute()
    if COMMIT: svc.edits().commit(packageName=PKG,editId=eid).execute(); print('COMMITTED',langs)
    else: svc.edits().delete(packageName=PKG,editId=eid).execute(); print('validated (dry)',langs)
except Exception:
    try: svc.edits().delete(packageName=PKG,editId=eid).execute()
    except Exception: pass
    raise
