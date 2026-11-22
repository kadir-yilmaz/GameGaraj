#!/usr/bin/env bash
# ==============================================================================
# .SYNOPSIS
#     GameGaraj .NET 8 Servisleri Yonetim Scripti (11 Servis - macOS)
#
# .DESCRIPTION
#     Bu script, projede yer alan 11 .NET 8 mikroservisini (Catalog, PhotoStock,
#     Basket, Order, Payment, Invoice, Campaign, Review, Discussion, Gateway ve
#     WebUI) kolayca baslatmanizi, durdurmanizi, yeniden baslatmanizi, durumlarini
#     ve canli loglarini izlemenizi saglar.
#     WebUI HTTPS (:7050 / :5050), diger 10 servis HTTP uzerinden baslatilir.
#     Zombie/orphan process olusmamasi icin pre-build + dotnet exec + detach kullanir.
#
# .EXAMPLE
#     ./dotnet-services.sh                   # Interaktif TUI menuyu acar
#     ./dotnet-services.sh start             # Tum 11 .NET servisini baslatir
#     ./dotnet-services.sh stop              # Tum .NET servislerini durdurur
#     ./dotnet-services.sh restart           # Tum .NET servislerini yeniden baslatir
#     ./dotnet-services.sh status            # Servislerin port, PID ve RAM durumunu gosterir
#     ./dotnet-services.sh start webui       # Sadece WebUI'yi (HTTPS :7050) baslatir
#     ./dotnet-services.sh restart order     # Sadece Order API'yi (:5015) yeniden baslatir
#     ./dotnet-services.sh stop catalog      # Sadece Catalog API'yi (:5011) durdurur
#     ./dotnet-services.sh logs webui        # WebUI canli loglarini izler
# ==============================================================================

set -u

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_DIR="$ROOT_DIR/ConsoleLogs"
PID_DIR="$LOG_DIR/.pids"
mkdir -p "$LOG_DIR" "$PID_DIR"

# Renkler
BOLD=$'\033[1m'
DIM=$'\033[2m'
RED=$'\033[0;31m'
GREEN=$'\033[0;32m'
YELLOW=$'\033[1;33m'
BLUE=$'\033[0;34m'
CYAN=$'\033[0;36m'
NC=$'\033[0m'

# Format: "key|gorunen_ad|proje_dizini|port"
SERVICES=(
  "catalog|Catalog API|GameGaraj.Catalog.API|5011"
  "photostock|PhotoStock API|GameGaraj.PhotoStock.API|5012"
  "basket|Basket API|GameGaraj.Basket.API|5013"
  "order|Order API|GameGaraj.Order.API|5015"
  "payment|Payment API|GameGaraj.Payment.API|5016"
  "invoice|Invoice API|GameGaraj.Invoice.API|5017"
  "campaign|Campaign API|GameGaraj.Campaign.API|5018"
  "review|Review API|GameGaraj.Review.API|5221"
  "discussion|Discussion API|GameGaraj.Discussion.API|5023"
  "gateway|Gateway|GameGaraj.Gateway|5000"
  "webui|WebUI|GameGaraj.WebUI|7050"
)

find_service_entry() {
  local target="$1"
  for entry in "${SERVICES[@]}"; do
    IFS='|' read -r key name dir port <<< "$entry"
    if [[ "$key" == "$target" ]]; then
      echo "$entry"
      return 0
    fi
  done
  return 1
}

get_pid_by_port() {
  local port="$1"
  local pid
  for pid in $(lsof -nP -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null); do
    local comm
    comm="$(ps -o comm= -p "$pid" 2>/dev/null || true)"
    if [[ "$comm" == *"dotnet"* || "$comm" == *"GameGaraj"* ]]; then
      echo "$pid"
      return 0
    fi
  done
  return 1
}

get_service_pid() {
  local key="$1"
  local port="$2"
  local pidfile="$PID_DIR/dotnet-${key}.pid"

  local port_pid
  port_pid="$(get_pid_by_port "$port")"
  if [[ -n "$port_pid" ]]; then
    echo "$port_pid"
    return 0
  fi

  if [[ -f "$pidfile" ]]; then
    local saved_pid
    saved_pid="$(cat "$pidfile" 2>/dev/null || true)"
    if [[ -n "$saved_pid" ]] && kill -0 "$saved_pid" 2>/dev/null; then
      local state
      state="$(ps -o state= -p "$saved_pid" 2>/dev/null | tr -d ' ')"
      if [[ "$state" != Z* && -n "$state" ]]; then
        echo "$saved_pid"
        return 0
      fi
    fi
    rm -f "$pidfile"
  fi

  return 1
}

get_ram_mb() {
  local pid="$1"
  local rss_kb
  rss_kb="$(ps -o rss= -p "$pid" 2>/dev/null | tr -d ' ')"
  if [[ -n "$rss_kb" && "$rss_kb" =~ ^[0-9]+$ ]]; then
    awk "BEGIN { printf \"%.1f\", $rss_kb / 1024 }"
  else
    echo "0.0"
  fi
}

kill_tree_clean() {
  local pid="$1"
  [[ -z "$pid" ]] && return 0

  # Alt process'leri bul
  local children
  children="$(pgrep -P "$pid" 2>/dev/null || true)"
  for c in $children; do
    kill_tree_clean "$c"
  done

  if kill -0 "$pid" 2>/dev/null; then
    kill -TERM "$pid" 2>/dev/null || true
    local i=0
    while kill -0 "$pid" 2>/dev/null && (( i < 30 )); do
      sleep 0.1
      ((i++))
    done
    if kill -0 "$pid" 2>/dev/null; then
      kill -KILL "$pid" 2>/dev/null || true
    fi
  fi
}

print_banner() {
  echo ""
  echo "${CYAN}${BOLD}======================================================================${NC}"
  echo "${CYAN}${BOLD}  GameGaraj .NET 8 Mikroservis Yoneticisi (11 Servis - macOS)         ${NC}"
  echo "${CYAN}${BOLD}======================================================================${NC}"
}

show_status() {
  print_banner
  printf "\n  ${BOLD}%-3s %-13s %-16s %-10s %-7s %-8s %-10s${NC}\n" "#" "Anahtar" "Servis" "Durum" "Port" "PID" "RAM"
  echo "  ${DIM}--------------------------------------------------------------------${NC}"

  local idx=1
  local running=0
  for entry in "${SERVICES[@]}"; do
    IFS='|' read -r key name dir port <<< "$entry"
    local pid
    if pid="$(get_service_pid "$key" "$port")"; then
      local ram
      ram="$(get_ram_mb "$pid")"
      printf "  %-3d %-13s %-16s ${GREEN}%-10s${NC} %-7s %-8s %s MB\n" "$idx" "$key" "$name" "AKTIF" "$port" "$pid" "$ram"
      ((running++))
    else
      printf "  %-3d %-13s %-16s ${RED}%-10s${NC} %-7s %-8s -\n" "$idx" "$key" "$name" "KAPALI" "$port" "-"
    fi
    ((idx++))
  done

  echo "  ${DIM}--------------------------------------------------------------------${NC}"
  echo "  ${BOLD}Aktif:${NC} ${GREEN}${running}${NC} / ${#SERVICES[@]}   ${DIM}Loglar: ConsoleLogs/<servis>.log${NC}"
  echo ""
}

build_solution_if_needed() {
  echo "  ${BLUE}[BUILD]${NC} .NET projeleri derleniyor..."
  if dotnet build "$ROOT_DIR/GameGaraj.sln" --nologo -v q; then
    echo "  ${GREEN}[OK]${NC} Derleme tamamlandi."
    return 0
  else
    echo "  ${RED}[HATA]${NC} Solution derlenemedi. Hatalari kontrol edin."
    return 1
  fi
}

start_one() {
  local entry="$1"
  local skip_build="${2:-0}"
  IFS='|' read -r key name dir port <<< "$entry"

  local existing
  if existing="$(get_service_pid "$key" "$port")"; then
    echo "  ${YELLOW}[!]${NC} $name zaten calisiyor (Port: $port | PID: $existing)"
    return 0
  fi

  local proj_dir="$ROOT_DIR/$dir"
  local dll_path="$proj_dir/bin/Debug/net8.0/$dir.dll"
  local log_file="$LOG_DIR/${key}.log"
  local pid_file="$PID_DIR/dotnet-${key}.pid"

  if [[ "$skip_build" == "0" || ! -f "$dll_path" ]]; then
    echo "  ${BLUE}[BUILD]${NC} $name derleniyor..."
    if ! dotnet build "$proj_dir/$dir.csproj" --nologo -v q; then
      echo "  ${RED}[HATA]${NC} $name derlenemedi."
      return 1
    fi
  fi

  local urls="http://localhost:$port"
  if [[ "$key" == "webui" ]]; then
    urls="https://localhost:7050;http://localhost:5050"
  fi

  echo "  ${CYAN}[>]${NC} $name baslatiliyor ($urls)..."
  : > "$log_file"

  # Subshell icinde nohup + background: subshell hemen kapanir, process PID 1'e baglanir (zombie olusmaz)
  (
    cd "$proj_dir" || exit 1
    ASPNETCORE_ENVIRONMENT=Development \
    ASPNETCORE_URLS="$urls" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    nohup dotnet exec "$dll_path" </dev/null >>"$log_file" 2>&1 &
    echo $! > "$pid_file"
  )

  # Portun acilmasini kisa sure bekle
  local i=0
  while (( i < 20 )); do
    if get_pid_by_port "$port" >/dev/null 2>&1; then
      local live_pid
      live_pid="$(get_pid_by_port "$port")"
      echo "$live_pid" > "$pid_file"
      echo "  ${GREEN}[OK]${NC} $name baslatildi (Port: $port | PID: $live_pid)"
      return 0
    fi
    sleep 0.2
    ((i++))
  done

  if [[ -f "$pid_file" ]] && kill -0 "$(cat "$pid_file")" 2>/dev/null; then
    echo "  ${YELLOW}[OK]${NC} $name baslatildi (PID: $(cat "$pid_file")), port aciliyor..."
  else
    echo "  ${RED}[HATA]${NC} $name baslatilamadi! Son log: $log_file"
    tail -n 5 "$log_file" 2>/dev/null | sed 's/^/      /'
    rm -f "$pid_file"
    return 1
  fi
}

stop_one() {
  local entry="$1"
  IFS='|' read -r key name dir port <<< "$entry"
  local pid_file="$PID_DIR/dotnet-${key}.pid"

  local pids=()
  if [[ -f "$pid_file" ]]; then
    local sp
    sp="$(cat "$pid_file" 2>/dev/null || true)"
    [[ -n "$sp" ]] && pids+=("$sp")
  fi

  local port_pid
  port_pid="$(get_pid_by_port "$port" || true)"
  [[ -n "$port_pid" ]] && pids+=("$port_pid")

  # Tekillestir
  local unique_pids
  unique_pids="$(printf "%s\n" "${pids[@]:-}" | awk 'NF && !seen[$0]++')"

  if [[ -z "$unique_pids" ]]; then
    rm -f "$pid_file"
    echo "  ${DIM}[-] $name zaten kapali.${NC}"
    return 0
  fi

  for p in $unique_pids; do
    echo "  ${YELLOW}[x]${NC} $name durduruluyor (PID: $p)..."
    kill_tree_clean "$p"
  done

  rm -f "$pid_file"
  echo "  ${GREEN}[OK]${NC} $name durduruldu."
}

start_all() {
  print_banner
  echo ""
  build_solution_if_needed || return 1
  echo ""
  for entry in "${SERVICES[@]}"; do
    start_one "$entry" 1
  done
  show_status
}

stop_all() {
  print_banner
  echo ""
  for entry in "${SERVICES[@]}"; do
    stop_one "$entry"
  done
  show_status
}

show_logs() {
  local target="$1"
  local entry
  if ! entry="$(find_service_entry "$target")"; then
    echo "${RED}Bilinmeyen servis: $target${NC}"
    return 1
  fi
  IFS='|' read -r key name dir port <<< "$entry"
  local log_file="$LOG_DIR/${key}.log"
  if [[ ! -f "$log_file" ]]; then
    echo "${YELLOW}$name icin henuz log dosyasi yok ($log_file).${NC}"
    return 1
  fi
  echo "${CYAN}=== $name Canli Loglari (Cikis: Ctrl+C) ===${NC}"
  tail -n 40 -f "$log_file"
}

interactive_menu() {
  while true; do
    clear 2>/dev/null || true
    show_status
    echo "  ${BOLD}Islemler:${NC}"
    echo "    ${GREEN}1${NC}) Tumunu Baslat (11 Servis)"
    echo "    ${RED}2${NC}) Tumunu Durdur"
    echo "    ${YELLOW}3${NC}) Tumunu Yeniden Baslat"
    echo "    ${CYAN}4${NC}) Tekil Servis Baslat"
    echo "    ${CYAN}5${NC}) Tekil Servis Durdur"
    echo "    ${CYAN}6${NC}) Tekil Servis Yeniden Baslat"
    echo "    ${BLUE}7${NC}) Canli Log Izle"
    echo "    ${DIM}8) Durumu Yenile   0) Cikis${NC}"
    echo ""
    read -r -p "  Seciminiz: " choice

    case "$choice" in
      1) start_all; read -r -p "  Devam etmek icin Enter'a basin..." _ ;;
      2) stop_all; read -r -p "  Devam etmek icin Enter'a basin..." _ ;;
      3) stop_all; start_all; read -r -p "  Devam etmek icin Enter'a basin..." _ ;;
      4)
        read -r -p "  Baslatilacak servis anahtari (orn: catalog, webui): " svc
        if entry="$(find_service_entry "$svc")"; then start_one "$entry" 0; else echo "  ${RED}Gecersiz anahtar.${NC}"; fi
        read -r -p "  Devam etmek icin Enter'a basin..." _
        ;;
      5)
        read -r -p "  Durdurulacak servis anahtari (orn: catalog, webui): " svc
        if entry="$(find_service_entry "$svc")"; then stop_one "$entry"; else echo "  ${RED}Gecersiz anahtar.${NC}"; fi
        read -r -p "  Devam etmek icin Enter'a basin..." _
        ;;
      6)
        read -r -p "  Yeniden baslatilacak servis anahtari: " svc
        if entry="$(find_service_entry "$svc")"; then stop_one "$entry"; start_one "$entry" 0; else echo "  ${RED}Gecersiz anahtar.${NC}"; fi
        read -r -p "  Devam etmek icin Enter'a basin..." _
        ;;
      7)
        read -r -p "  Log izlenecek servis anahtari (orn: webui, order): " svc
        show_logs "$svc" || true
        read -r -p "  Devam etmek icin Enter'a basin..." _
        ;;
      8) ;;
      0|q|Q) echo ""; exit 0 ;;
      *) ;;
    esac
  done
}

# CLI veya Interaktif Mod
if [[ $# -eq 0 ]]; then
  interactive_menu
  exit 0
fi

ACTION="$1"
TARGET="${2:-all}"

case "$ACTION" in
  start)
    if [[ "$TARGET" == "all" ]]; then
      start_all
    elif entry="$(find_service_entry "$TARGET")"; then
      start_one "$entry" 0
    else
      echo "${RED}Bilinmeyen servis: $TARGET${NC}"
      exit 1
    fi
    ;;
  stop)
    if [[ "$TARGET" == "all" ]]; then
      stop_all
    elif entry="$(find_service_entry "$TARGET")"; then
      stop_one "$entry"
    else
      echo "${RED}Bilinmeyen servis: $TARGET${NC}"
      exit 1
    fi
    ;;
  restart)
    if [[ "$TARGET" == "all" ]]; then
      stop_all
      start_all
    elif entry="$(find_service_entry "$TARGET")"; then
      stop_one "$entry"
      start_one "$entry" 0
    else
      echo "${RED}Bilinmeyen servis: $TARGET${NC}"
      exit 1
    fi
    ;;
  status)
    show_status
    ;;
  logs)
    show_logs "$TARGET"
    ;;
  *)
    echo "Kullanim: $0 [start|stop|restart|status|logs] [all|catalog|photostock|basket|order|payment|invoice|campaign|review|discussion|gateway|webui]"
    exit 1
    ;;
esac
