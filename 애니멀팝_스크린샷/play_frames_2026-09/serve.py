import http.server, sys, os
ROOT=sys.argv[1]; PORT=int(sys.argv[2])
TYPES={'.js':'application/javascript','.wasm':'application/wasm','.data':'application/octet-stream','.json':'application/json'}
class H(http.server.SimpleHTTPRequestHandler):
    def __init__(self,*a,**k): super().__init__(*a,directory=ROOT,**k)
    def end_headers(self):
        p=self.path.split('?')[0]
        if p.endswith('.br'):
            self.send_header('Content-Encoding','br')
        self.send_header('Cache-Control','no-store')
        super().end_headers()
    def guess_type(self,path):
        p=path[:-3] if path.endswith('.br') else path
        return TYPES.get(os.path.splitext(p)[1]) or super().guess_type(p)
    def log_message(self,*a): pass
http.server.ThreadingHTTPServer(('127.0.0.1',PORT),H).serve_forever()
