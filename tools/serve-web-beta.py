"""Serve the Unity browser build locally with its compression headers."""
import argparse
import functools
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path


class UnityBuildHandler(SimpleHTTPRequestHandler):
    def guess_type(self, path):
        uncompressed = path.removesuffix(".gz").removesuffix(".br")
        if uncompressed.endswith(".wasm"):
            return "application/wasm"
        if uncompressed.endswith(".js"):
            return "application/javascript"
        if uncompressed.endswith(".data"):
            return "application/octet-stream"
        return super().guess_type(uncompressed)

    def end_headers(self):
        if self.path.split("?", 1)[0].endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        elif self.path.split("?", 1)[0].endswith(".br"):
            self.send_header("Content-Encoding", "br")
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=18080)
    parser.add_argument("--directory", default="Builds/WebGLBeta")
    args = parser.parse_args()
    root = Path(args.directory).resolve()
    handler = functools.partial(UnityBuildHandler, directory=str(root))
    print(f"Serving {root} at http://127.0.0.1:{args.port}", flush=True)
    ThreadingHTTPServer(("127.0.0.1", args.port), handler).serve_forever()
