"""Verify the beta's HTTP entry point and compression headers; not a browser runtime test."""
import argparse
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urljoin
from urllib.request import Request, urlopen


def check(base_url):
    with urlopen(base_url, timeout=15) as response:
        html = response.read().decode("utf-8")
    if "{{{" in html or 'id="unity-canvas"' not in html:
        raise RuntimeError("Response is not a compiled Unity entry page")
    files = sorted(set(re.findall(r'["\']((?:Build|TemplateData)/[^"\']+)["\']', html)))
    if not any(".wasm" in name for name in files) or not any(".loader.js" in name for name in files):
        raise RuntimeError("WASM or loader URL is absent")
    results = []
    for name in files:
        with urlopen(Request(urljoin(base_url, name), method="HEAD"), timeout=15) as response:
            mime = response.headers.get_content_type()
            encoding = response.headers.get("Content-Encoding", "")
            expected_encoding = "br" if name.endswith(".br") else "gzip" if name.endswith(".gz") else ""
            plain_name = name.removesuffix(".br").removesuffix(".gz")
            expected_mime = "application/wasm" if plain_name.endswith(".wasm") else None
            if plain_name.endswith(".js"):
                expected_mime = "application/javascript"
            valid = (response.status == 200 and encoding == expected_encoding
                     and (expected_mime is None or mime == expected_mime)
                     and int(response.headers.get("Content-Length", 0)) > 0)
            results.append({"file": name, "status": response.status, "contentType": mime,
                            "contentEncoding": encoding, "bytes": int(response.headers.get("Content-Length", 0)),
                            "passed": valid})
    return {"checkedUtc": datetime.now(timezone.utc).isoformat(), "url": base_url,
            "scope": "HTTP only; Unity rendering and runtime errors require browser validation",
            "passed": all(item["passed"] for item in results), "files": results}


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--url", default="http://127.0.0.1:18123/")
    parser.add_argument("--report", default="docs/Validation/BetaPolishWeb/http-check.json")
    args = parser.parse_args()
    result = check(args.url.rstrip("/") + "/")
    report_path = Path(args.report)
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    raise SystemExit(0 if result["passed"] else 1)
