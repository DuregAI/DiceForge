from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
import os
os.chdir('Builds/Glimblehop-0.3.8-release')
class Handler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        plain = path[:-3] if path.endswith('.br') else path
        if plain.endswith('.wasm'): return 'application/wasm'
        if plain.endswith('.js'): return 'application/javascript'
        if plain.endswith('.data'): return 'application/octet-stream'
        return super().guess_type(plain)
    def end_headers(self):
        if self.path.endswith('.br'): self.send_header('Content-Encoding','br')
        super().end_headers()
ThreadingHTTPServer(('127.0.0.1', 8038), Handler).serve_forever()
