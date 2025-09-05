import json
import os
import time
import random
from pathlib import Path
from typing import Optional, Tuple

import requests
from rich.console import Console
import socket
import platform
import uuid
import logging

console = Console()

class _OnlyThisFile(logging.Filter):
    def filter(self, record: logging.LogRecord) -> bool:
        try:
            p = record.pathname.replace('\\', '/').lower()
            return p.endswith('cli-app/main_selfbot.py')
        except Exception:
            return True


def _default_log_path() -> str:
    try:
        appdata = os.environ.get("APPDATA")
        if appdata:
            base = Path(appdata) / "JellyfinDiscordRPC"
        else:
            base = Path.home() / ".config" / "jellyfin-discord-rpc"
        base.mkdir(parents=True, exist_ok=True)
        return str(base / "rpc_selfbot.log")
    except Exception:
        return str(Path.cwd() / "rpc_selfbot.log")


def setup_logging() -> None:
    level = logging.INFO
    root = logging.getLogger()
    root.handlers.clear()
    root.setLevel(level)

    fmt_file = logging.Formatter('%(asctime)s [%(levelname)s] %(message)s')
    flt = _OnlyThisFile()

    log_file = os.environ.get("LOG_FILE") or _default_log_path()
    try:
        fh = logging.FileHandler(log_file, encoding='utf-8')
        fh.setLevel(level)
        fh.setFormatter(fmt_file)
        fh.addFilter(flt)
        root.addHandler(fh)
    except Exception:
        pass

    # Quiet noisy libraries
    logging.getLogger('requests').setLevel(logging.WARNING)
    logging.getLogger('urllib3').setLevel(logging.WARNING)


def _config_paths() -> Tuple[Path, Path]:
    here = Path(__file__).resolve().parent
    local = here / "config.json"
    appdata = os.environ.get("APPDATA")
    if appdata:
        roaming = Path(appdata) / "JellyfinDiscordRPC" / "config.json"
    else:
        roaming = Path.home() / ".config" / "jellyfin-discord-rpc" / "config.json"
    return local, roaming


def load_config() -> dict:
    local, roaming = _config_paths()
    for p in (local, roaming):
        if p.exists():
            try:
                with p.open("r", encoding="utf-8") as f:
                    cfg = json.load(f)
                    return cfg
            except Exception as e:
                console.print(f"[red]Failed to read config at {p}: {e}[/red]")

    # Fallback to env vars
    cfg = {
        "jellyfin_url": os.environ.get("JELLYFIN_URL"),
        "api_key": os.environ.get("JELLYFIN_API_KEY"),
        "discord_server_url": os.environ.get("DISCORD_SERVER_URL", "http://localhost:3001"),
        "interval": float(os.environ.get("POLL_INTERVAL", "5")),
        "Images": {"ENABLE_IMAGES": True},
    }

    if not cfg["jellyfin_url"] or not cfg["api_key"]:
        # Write example to roaming and instruct user
        example = {
            "jellyfin_url": "https://your.jellyfin",
            "api_key": "YOUR_API_KEY",
            "discord_server_url": "http://localhost:3001",
            "interval": 5
        }
        try:
            roaming.parent.mkdir(parents=True, exist_ok=True)
            with roaming.open("w", encoding="utf-8") as f:
                json.dump(example, f, indent=2)
        except Exception:
            pass
        console.print(
            "[yellow]Missing config. Create config.json at one of:[/yellow]\n"
            f" - {local}\n"
            f" - {roaming}\n"
            "or set JELLYFIN_URL and JELLYFIN_API_KEY environment variables."
        )
        raise SystemExit(1)

    return cfg


def get_presence(base_url: str, api_key: str, username: Optional[str] = None) -> Optional[dict]:
    device_name = socket.gethostname()
    device_id = uuid.uuid5(uuid.NAMESPACE_DNS, device_name).hex
    client_name = "Jellyfin-Discord-RPC-Selfbot"
    client_version = "2.0"
    os_name = platform.system()
    headers = {
        "X-Emby-Token": api_key,
        "X-Emby-Client": client_name,
        "X-Emby-Client-Version": client_version,
        "X-Emby-Device-Name": device_name,
        "X-Emby-Device-Id": device_id,
        "X-Emby-OperatingSystem": os_name,
        "X-Emby-Authorization": f"MediaBrowser Client=\"{client_name}\", Device=\"{device_name}\", DeviceId=\"{device_id}\", Version=\"{client_version}\", Token=\"{api_key}\"",
        "Authorization": f"MediaBrowser Token=\"{api_key}\"",
        "Accept": "application/json",
        "User-Agent": f"{client_name}/{client_version}"
    }
    url = base_url.rstrip("/") + "/Plugins/DiscordRpc/Presence/Me"
    params = {"api_key": api_key}
    if username:
        params["username"] = username
    try:
        resp = requests.get(url, headers=headers, params=params, timeout=10)
        if resp.status_code == 401:
            console.print("[red]Unauthorized: check your Jellyfin API key[/red]")
            logging.warning("Unauthorized (401) from Presence endpoint")
            return None
        resp.raise_for_status()
        return resp.json()
    except Exception as e:
        logging.error(f"Error fetching presence from {url}: {e}", exc_info=True)
        console.print(f"[red]Error fetching presence: {e}[/red]")
        return None


def update_discord_presence(discord_server_url: str, presence_data: dict) -> bool:
    """Send presence data to Discord selfbot server"""
    try:
        url = discord_server_url.rstrip("/") + "/update-presence"
        response = requests.post(url, json=presence_data, timeout=10)
        
        if response.status_code == 503:
            console.print("[yellow]Discord selfbot server not ready[/yellow]")
            return False
        
        response.raise_for_status()
        result = response.json()
        
        if result.get("success"):
            action = result.get("action", "unknown")
            if action == "updated":
                logging.info(f"Discord presence updated: {presence_data.get('details', 'N/A')}")
            elif action == "cleared":
                logging.info("Discord presence cleared")
            return True
        else:
            logging.error(f"Failed to update Discord presence: {result}")
            return False
            
    except Exception as e:
        logging.error(f"Error updating Discord presence: {e}", exc_info=True)
        console.print(f"[red]Error updating Discord presence: {e}[/red]")
        return False


def check_discord_server(discord_server_url: str) -> bool:
    """Check if Discord selfbot server is running"""
    try:
        url = discord_server_url.rstrip("/") + "/health"
        response = requests.get(url, timeout=5)
        response.raise_for_status()
        
        data = response.json()
        if data.get("discord_ready"):
            console.print(f"[green]✅ Connected to Discord selfbot server ({data.get('user', 'Unknown user')})[/green]")
            return True
        else:
            console.print("[yellow]⚠️ Discord selfbot server running but Discord not ready[/yellow]")
            return False
            
    except Exception as e:
        console.print(f"[red]❌ Cannot connect to Discord selfbot server: {e}[/red]")
        console.print(f"[yellow]Make sure the server is running at {discord_server_url}[/yellow]")
        return False


def main() -> None:
    setup_logging()
    cfg = load_config()
    logging.info("Starting Jellyfin Discord RPC selfbot client")
    logging.info(f"Server URL: {cfg.get('jellyfin_url')}")
    
    jellyfin_url = cfg.get("jellyfin_url")
    api_key = cfg.get("api_key")
    username = (cfg.get("username") or "").strip()
    discord_server_url = cfg.get("discord_server_url", "http://localhost:3001")
    interval = float(cfg.get("interval", 5))

    # Clear console on start and print header
    try:
        os.system('cls' if os.name == 'nt' else 'clear')
    except Exception:
        pass
    
    header = "Jellyfin Discord RPC Selfbot" + (f" (user: {username})" if username else "")
    console.print(f"[bold cyan]{header}[/bold cyan]")
    
    # Check Discord server connectivity
    if not check_discord_server(discord_server_url):
        console.print("[red]Please start the Discord selfbot server first![/red]")
        console.print(f"[yellow]Run: cd discord-server && npm start[/yellow]")
        raise SystemExit(1)
    
    logging.info(f"Username scope: {username or '(none)'}")
    logging.info(f"Discord server: {discord_server_url}")

    # Helpers for nicer TTY output
    def set_title(title: str) -> None:
        try:
            if os.name == 'nt':
                os.system(f"title {title}")
            else:
                print(f"\33]0;{title}\a", end="", flush=True)
        except Exception:
            pass

    set_title("Jellyfin RPC Selfbot - Idle" + (f" (user: {username})" if username else ""))

    last_payload = None
    paused_since: float | None = None
    long_pause = False
    last_content_key: Optional[str] = None

    # Initial small randomized delay to avoid stampeding herd when many clients start
    time.sleep(random.uniform(0, min(2.0, interval)))

    while True:
        data = get_presence(jellyfin_url, api_key, username=username or None)
        if not data:
            time.sleep(interval + random.uniform(0, 0.5 * max(0.1, interval)))
            continue

        # Optional username scoping: if server can't resolve user from token
        if username:
            owner = (data.get("user_name") or "").strip().lower()
            if owner and owner != username.lower():
                # Skip updates that aren't for this username
                time.sleep(interval)
                continue

        if not data.get("active"):
            if last_payload is not None:
                update_discord_presence(discord_server_url, {"active": False})
                last_payload = None
            paused_since = None
            long_pause = False
            content_key = "idle"
            if content_key != last_content_key:
                console.print("[dim]Idle[/dim]")
                set_title("Jellyfin RPC Selfbot - Idle")
                last_content_key = content_key
            time.sleep(interval + random.uniform(0, 0.5 * max(0.1, interval)))
            continue

        # Handle paused timing and long-pause clearing
        if data.get("is_paused"):
            if paused_since is None:
                paused_since = time.time()
            paused_elapsed = time.time() - paused_since
            if paused_elapsed >= 180:  # 3 minutes
                if last_payload is not None:
                    update_discord_presence(discord_server_url, {"active": False})
                    last_payload = None
                long_pause = True
            else:
                long_pause = False
        else:
            paused_since = None
            long_pause = False

        # Show content change once
        content_key = str(data.get("item_id") or data.get("details") or "unknown")
        if content_key != last_content_key:
            title_line = data.get("details") or ""
            state_line = data.get("state") or ""
            media_type = data.get("item_type") or "Unknown"
            
            # Do not clear on change; just print a fresh section so logs remain visible
            header = "Jellyfin Discord RPC Selfbot" + (f" (user: {username})" if username else "")
            console.print(f"\n[bold cyan]{header}[/bold cyan]")
            
            # Show media type for debugging
            type_color = "green" if media_type == "Episode" else "blue" if media_type == "Movie" else "yellow"
            console.print(f"[{type_color}]Media Type: {media_type}[/{type_color}]")
            
            logging.info(f"Now playing ({media_type}): {title_line} | {state_line}")
            if title_line:
                console.print(f"[bold]{title_line}[/bold]")
                set_title(f"{title_line}")
            if state_line:
                for ln in str(state_line).split("\n"):
                    if ln:
                        console.print(ln)
            last_content_key = content_key

        if not long_pause and data != last_payload:
            success = update_discord_presence(discord_server_url, data)
            if success:
                last_payload = data

        # Backoff when paused; normal faster polling when playing
        if data.get("is_paused"):
            pause_delay = random.uniform(30.0, 45.0)
            time.sleep(pause_delay)
        else:
            time.sleep(interval + random.uniform(0, 0.5 * max(0.1, interval)))


if __name__ == "__main__":
    main()
