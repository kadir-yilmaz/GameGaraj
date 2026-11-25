import json
import os
import shutil
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parent
RESULTS_DIR = BASE_DIR / "results"
SUMMARY_JSON = RESULTS_DIR / "last_summary.json"
LAST_RESULT_TXT = RESULTS_DIR / "last_result.txt"


def collect_node_snapshot(name: str, host: str, user: str, password: str, extra_k8s: bool = False) -> str:
    try:
        import paramiko
    except ImportError:
        return f"[{name} ({host}) paramiko kurulu degil]"

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    try:
        client.connect(
            host,
            username=user,
            password=password,
            timeout=5,
            banner_timeout=5,
            auth_timeout=5,
            look_for_keys=True,
        )
        extra_cmd = ""
        if extra_k8s:
            extra_cmd = """
export KUBECONFIG=/home/kadirserver/.kube/config
echo ""
echo "=== k3s Node Durumu ==="
kubectl get nodes -o wide 2>/dev/null || true
echo ""
echo "=== k3s Node Kaynaklari (kubectl top nodes) ==="
kubectl top nodes 2>/dev/null || true
echo ""
echo "=== k3s GameGaraj Pod Kaynaklari (Top 15 CPU) ==="
kubectl top pods -n gamegaraj --sort-by=cpu 2>/dev/null | head -n 16 || true
echo ""
echo "=== k3s HPA Durumu ==="
kubectl get hpa -n gamegaraj 2>/dev/null || true
echo ""
echo "=== Dokploy DB/ES/Redis Stats ==="
docker stats --no-stream --format "table {{.Name}}\\t{{.CPUPerc}}\\t{{.MemUsage}}\\t{{.MemPerc}}" 2>/dev/null | grep -i -E "NAME|elastic|redis|postgres|mssql|keycloak" || true
"""
        cmd = f"""
echo "=== {name} ({host}) Uptime & RAM ==="
uptime
free -h
echo ""
echo "=== {name} Top CPU Processes (htop ozeti) ==="
ps -eo pid,user,%cpu,%mem,comm --sort=-%cpu | head -n 13
{extra_cmd}
"""
        _, stdout, _ = client.exec_command(cmd, timeout=12)
        return stdout.read().decode("utf-8", errors="replace").strip()
    except Exception as exc:
        return f"[{name} ({host}) snapshot alinamadi: {exc}]"
    finally:
        client.close()


def main():
    RESULTS_DIR.mkdir(parents=True, exist_ok=True)
    if not SUMMARY_JSON.exists():
        print(f"Hata: {SUMMARY_JSON} bulunamadi. Once k6 testini calistirin.")
        return

    summary = json.loads(SUMMARY_JSON.read_text(encoding="utf-8"))
    base_url = summary.get("base_url", "")
    is_lan = "192.168." in base_url
    password = os.getenv("CLUSTER_SSH_PASS") or os.getenv("KADIRSERVER_SSH_PASS") or "1234"

    nodes = [
        ("kadirserver", "192.168.1.56" if is_lan else os.getenv("KADIRSERVER_TAILSCALE_IP", "100.100.32.3"), os.getenv("KADIRSERVER_USER", "kadirserver"), True),
        ("wk1", "192.168.1.5" if is_lan else os.getenv("WK1_TAILSCALE_IP", "100.67.37.25"), "wk1", False),
        ("wk2", "192.168.1.200" if is_lan else os.getenv("WK2_TAILSCALE_IP", "100.120.194.97"), "wk2", False),
        ("wk3", "192.168.1.199" if is_lan else os.getenv("WK3_TAILSCALE_IP", "192.168.1.199"), "wk3", False),
    ]

    with ThreadPoolExecutor(max_workers=4) as pool:
        snapshots = list(pool.map(lambda n: collect_node_snapshot(n[0], n[1], n[2], password, n[3]), nodes))

    lat = summary.get("latency_ms", {})
    now_str = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    elapsed_s = (summary.get("run_duration_ms", 0) or 0) / 1000.0

    lines = [
        "=== GameGaraj k6 Test Sonucu ===",
        f"Tarih       : {now_str}",
        f"Hedef URL   : {base_url}",
        f"Hedef RPS   : {summary.get('target_rps')} | Sure Ayari: {summary.get('duration_config')}",
        f"Gecen Sure  : {elapsed_s:.1f} sn",
        f"Toplam Istek: {summary.get('total_requests')} | Basarisiz: {summary.get('failed_requests')} ({summary.get('fail_rate_pct', 0):.2f}%)",
        f"Dropped Iter: {summary.get('dropped_iterations')} | Max VU: {summary.get('vus_max')}",
        f"Gercek RPS  : {summary.get('actual_rps', 0):.1f}",
        f"Latency (ms): avg={lat.get('avg', 0):.1f} p50={lat.get('med', 0):.1f} p90={lat.get('p90', 0):.1f} p95={lat.get('p95', 0):.1f} max={lat.get('max', 0):.1f}",
        f"Trafik      : Alinan={summary.get('data_received_mb', 0):.2f} MB | Gonderilen={summary.get('data_sent_mb', 0):.2f} MB",
        "",
        *snapshots,
    ]
    report_text = "\n".join(lines) + "\n"

    LAST_RESULT_TXT.write_text(report_text, encoding="utf-8")
    run_dir = RESULTS_DIR / datetime.now().strftime("%Y%m%d-%H%M%S")
    run_dir.mkdir(parents=True, exist_ok=True)
    (run_dir / "result.txt").write_text(report_text, encoding="utf-8")
    shutil.copy2(SUMMARY_JSON, run_dir / "summary.json")

    print(f"Rapor kaydedildi: {LAST_RESULT_TXT}")
    print(f"Arsivlendi      : {run_dir / 'result.txt'}")


if __name__ == "__main__":
    main()
