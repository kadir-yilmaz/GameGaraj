#!/usr/bin/env bash
# ==============================================================================
# .SYNOPSIS
#     GameGaraj Go Servisleri Yonetim Scripti (Search API & Notification API - macOS)
#
# .DESCRIPTION
#     Bu script, projede yer alan Go mikroservislerini (Search API :5082 ve
#     Notification API :5025) kolayca baslatmanizi, durdurmanizi, yeniden
#     baslatmanizi, durumlarini ve canli loglarini izlemenizi saglar.
#     Zombie/orphan process olusmamasi icin derlenmis binary + detach kullanir.
#
# .EXAMPLE
#     ./go-services.sh                       # Interaktif TUI menuyu acar
#     ./go-services.sh start                 # Tum Go servislerini baslatir
#     ./go-services.sh stop                  # Tum Go servislerini durdurur
#     ./go-services.sh restart               # Tum Go servislerini yeniden baslatir
#     ./go-services.sh status                # Servislerin port, PID ve RAM durumunu gosterir
#     ./go-services.sh start search          # Sadece Search API'yi (:5082) baslatir
#     ./go-services.sh stop notification     # Sadece Notification API'yi (:5025) durdurur
#     ./go-services.sh logs notification     # Notification API canli loglarini izler
# ==============================================================================

set -u

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOG_DIR="$ROOT_DIR/ConsoleLogs"
PID_DIR="$LOG_DIR/.pids"
BIN_DIR="$LOG_DIR/.bin"
mkdir -p "$LOG_DIR" "$PID_DIR" "$BIN_DIR"

# Renkler
BOLD=$'\033[1m'
DIM=$'\033[2m'
RED=$'\033[0;31m'
GREEN=$'\033[0;32m'
YELLOW=$'\033[1;33m'
BLUE=$'\033[0;34m'
CYAN=$'\033[0;36m'
NC=$'\033[0m'

# Format: "key|gorunen_ad|dizin|main_dosyasi|binary_adi|port"
SERVICES=(
  "search|Search API|GameGaraj.Search.API|cmd/server/main.go|search-api|5082"
  "notification|Notification API|GameGaraj.Notification.API|cmd/api/main.go|notif-api|5025"
)

normalize_key() {
  case "$1" in
    notif|notification) echo "notification" ;;
    search) echo "search" ;;
    *) echo "$1" ;;
  esac
}

find_service_entry() {
  local target
  target="$(normalize_key "$1")"
  for entry in "${SERVICES[@]}"; do
    IFS='|' read -r key name dir main_file bin_name port <<< "$entry"
    if [[ "$key" == "$target" ]]; then
      echo "$entry"
      return 0
    fi
  done
  return 1
}

get_pid_by_port() {
  local port="$1"
  lsof -nP -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null | head -n 1
}

get_service_pid() {
  local key="$1"
  local port="$2"
  local pidfile="$PID_DIR/go-${key}.pid"

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
  echo "${CYAN}${BOLD}  GameGaraj Go Mikroservis Yoneticisi (2 Servis - macOS)              ${NC}"
  echo "${CYAN}${BOLD}======================================================================${NC}"
}

show_status() {
  print_banner
  printf "\n  ${BOLD}%-3s %-14s %-18s %-10s %-7s %-8s %-10s${NC}\n" "#" "Anahtar" "Servis" "Durum" "Port" "PID" "RAM"
  echo "  ${DIM}--------------------------------------------------------------------${NC}"

  local idx=1
  local running=0
  for entry in "${SERVICES[@]}"; do
    IFS='|' read -r key name dir main_file bin_name port <<< "$entry"
    local pid
    if pid="$(get_service_pid "$key" "$port")"; then
      local ram
      ram="$(get_ram_mb "$pid")"
      printf "  %-3d %-14s %-18s ${GREEN}%-10s${NC} %-7s %-8s %s MB\n" "$idx" "$key" "$name" "AKTIF" "$port" "$pid" "$ram"
      ((running++))
    else
      printf "  %-3d %-14s %-18s ${RED}%-10s${NC} %-7s %-8s -\n" "$idx" "$key" "$name" "KAPALI" "$port" "-"
    fi
    ((idx++))
  done

  echo "  ${DIM}--------------------------------------------------------------------${NC}"
  echo "  ${BOLD}Aktif:${NC} ${GREEN}${running}${NC} / ${#SERVICES[@]}   ${DIM}Loglar: ConsoleLogs/<servis>-api.log${NC}"
  echo ""
}

start_one() {
  local entry="$1"
  IFS='|' read -r key name dir main_file bin_name port <<< "$entry"

  local existing
  if existing="$(get_service_pid "$key" "$port")"; then
    echo "  ${YELLOW}[!]${NC} $name zaten calisiyor (Port: $port | PID: $existing)"
    return 0
  fi

  local svc_dir="$ROOT_DIR/$dir"
  local bin_path="$BIN_DIR/$bin_name"
  local log_file="$LOG_DIR/${key}-api.log"
  local pid_file="$PID_DIR/go-${key}.pid"

  echo "  ${BLUE}[BUILD]${NC} $name derleniyor..."
  if ! (cd "$svc_dir" && go build -o "$bin_path" "$main_file"); then
    echo "  ${RED}[HATA]${NC} $name derlenemedi."
    return 1
  fi

  echo "  ${CYAN}[>]${NC} $name baslatiliyor (:$port)..."
  : > "$log_file"

  # .env varsa ortama yukle (SMTP vb. degiskenler icin)
  (
    cd "$svc_dir" || exit 1
    if [[ -f "$ROOT_DIR/.env" ]]; then
      set -a
      # shellcheck disable=SC1091
      source "$ROOT_DIR/.env"
      set +a
    fi
    nohup "$bin_path" </dev/null >>"$log_file" 2>&1 &
    echo $! > "$pid_file"
  )

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
  IFS='|' read -r key name dir main_file bin_name port <<< "$entry"
  local pid_file="$PID_DIR/go-${key}.pid"

  local pids=()
  if [[ -f "$pid_file" ]]; then
    local sp
    sp="$(cat "$pid_file" 2>/dev/null || true)"
    [[ -n "$sp" ]] && pids+=("$sp")
  fi

  local port_pids
  port_pids="$(lsof -nP -tiTCP:"$port" -sTCP:LISTEN 2>/dev/null || true)"
  for pp in $port_pids; do
    pids+=("$pp")
  done

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
  for entry in "${SERVICES[@]}"; do
    start_one "$entry"
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
    echo "${RED}Bilinmeyen servis: $target (search | notification)${NC}"
    return 1
  fi
  IFS='|' read -r key name dir main_file bin_name port <<< "$entry"
  local log_file="$LOG_DIR/${key}-api.log"
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
    echo "    ${GREEN}1${NC}) Tumunu Baslat (Search + Notification)"
    echo "    ${RED}2${NC}) Tumunu Durdur"
    echo "    ${YELLOW}3${NC}) Tumunu Yeniden Baslat"
    echo "    ${CYAN}4${NC}) Search API Baslat / Durdur"
    echo "    ${CYAN}5${NC}) Notification API Baslat / Durdur"
    echo "    ${BLUE}6${NC}) Canli Log Izle (search / notification)"
    echo "    ${DIM}7) Durumu Yenile   0) Cikis${NC}"
    echo ""
    read -r -p "  Seciminiz: " choice

    case "$choice" in
      1) start_all; read -r -p "  Devam etmek icin Enter'a basin..." _ ;;
      2) stop_all; read -r -p "  Devam etmek icin Enter'a basin..." _ ;;
      3) stop_all; start_all; read -r -p "  Devam etmek icin Enter'a basin..." _ ;;
      4)
        entry="$(find_service_entry search)"
        if get_service_pid search 5082 >/dev/null 2>&1; then stop_one "$entry"; else start_one "$entry"; fi
        read -r -p "  Devam etmek icin Enter'a basin..." _
        ;;
      5)
        entry="$(find_service_entry notification)"
        if get_service_pid notification 5025 >/dev/null 2>&1; then stop_one "$entry"; else start_one "$entry"; fi
        read -r -p "  Devam etmek icin Enter'a basin..." _
        ;;
      6)
        read -r -p "  Log izlenecek servis (search / notification): " svc
        show_logs "$svc" || true
        read -r -p "  Devam etmek icin Enter'a basin..." _
        ;;
      7) ;;
      0|q|Q) echo ""; exit 0 ;;
      *) ;;
    esac
  done
}

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
      start_one "$entry"
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
      start_one "$entry"
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
    echo "Kullanim: $0 [start|stop|restart|status|logs] [all|search|notification]"
    exit 1
    ;;
esac
