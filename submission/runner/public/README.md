# Public evaluator runs

Both reports come from the unchanged runner with a fresh state directory per case. Both runs passed **10/10 cases and 12/12 steps** with no differences, critical false clears, escalation errors or adapter failures.

| Run | Reports | Wall time | Slowest step |
| --- | --- | --- | --- |
| Self-contained Linux x64 | [JSON](native/eval-report.json) · [text](native/eval-report.txt) | 43.44 s | 5.036 s |
| Docker fallback | [JSON](docker/eval-report.json) · [text](docker/eval-report.txt) | 38.39 s | 4.974 s |

Recorded on Linux x86_64 with Docker Engine 29.1.2 and Python 3.12.3. Wall times include Python startup. Per-step times come from the evaluator. The runner's timeout was 30 seconds per step.

```sh
docker build -f Dockerfile.adapter --target export --build-arg RID=linux-x64 \
  --output type=local,dest=dist/linux-x64 .
docker build -f Dockerfile.adapter --target runtime -t invoice-adapter:local .
python3 -m venv .venv
.venv/bin/python -m pip install -r requirements.txt
.venv/bin/python tools/run_evals.py --adapter 'dist/linux-x64/InvoiceDesk.Adapter' \
  --report-dir submission/runner/public/native
.venv/bin/python tools/run_evals.py --adapter './scripts/adapter-docker' \
  --report-dir 'results/local/public docker fallback'
```

The Docker report files were copied from the path shown above. That relative path includes spaces. A separate two-step run reopened the SQLite state through the same launcher, and the host UID/GID owned the writable database. Both adapters returned exit code 2 with a single `VALIDATION_ERROR` JSON response for `{}` on stdin.

| Export | Inspection and execution |
| --- | --- |
| `linux-x64` | x86-64 ELF apphost and `libe_sqlite3.so`. Native and Docker fallback passed the runner. |
| `linux-arm64` | AArch64 ELF apphost and `libe_sqlite3.so`. Export inspected on Linux x64. |
| `osx-arm64` | arm64 Mach-O apphost and `libe_sqlite3.dylib`. Apphost and SQLite CodeDirectory flags include ad-hoc signing. |
| `osx-x64` | x86_64 Mach-O apphost and `libe_sqlite3.dylib`. Apphost CodeDirectory flags include ad-hoc signing. Intel SQLite dylib has no CodeDirectory. |
| `win-x64` | x86-64 PE apphost and `e_sqlite3.dll`. Export inspected on Linux x64. Native Windows is unsupported. |

The Mach-O inspection read the embedded signature metadata on Linux. Native macOS and Linux arm64 execution remain unverified.
