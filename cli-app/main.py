import json
import os
import time
import random
from pathlib import Path
from typing import Optional, Tuple

import requests
from pypresence import Presence
from rich.console import Console

console = Console()


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
        "discord_client_id": os.environ.get("DISCORD_CLIENT_ID", "1199810830972170261"),
        "interval": float(os.environ.get("POLL_INTERVAL", "5")),
    }

    if not cfg["jellyfin_url"] or not cfg["api_key"]:
        # Write example to roaming and instruct user
        example = {
            "jellyfin_url": "https://your.jellyfin",
            "api_key": "YOUR_API_KEY",
            "discord_client_id": "1199810830972170261",
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


def get_presence(base_url: str, api_key: str) -> Optional[dict]:
    headers = {
        "X-Emby-Token": api_key,
        "Accept": "application/json",
        "User-Agent": "Jellyfin-Discord-RPC-CLI/1.1"
    }
    url = base_url.rstrip("/") + "/Plugins/DiscordRpc/Presence/Me"
    try:
        resp = requests.get(url, headers=headers, timeout=10)
        if resp.status_code == 401:
            console.print("[red]Unauthorized: check your Jellyfin API key[/red]")
            return None
        resp.raise_for_status()
        return resp.json()
    except Exception as e:
        console.print(f"[red]Error fetching presence: {e}[/red]")
        return None


def main() -> None:
    cfg = load_config()
    jellyfin_url = cfg.get("jellyfin_url")
    api_key = cfg.get("api_key")
    discord_client_id = str(cfg.get("discord_client_id", "1199810830972170261"))
    interval = float(cfg.get("interval", 5))

    rpc = Presence(discord_client_id)
    try:
        rpc.connect()
    except Exception as e:
        console.print(f"[red]Failed to connect to Discord RPC: {e}[/red]")
        raise SystemExit(1)

    console.print("[green]Connected to Discord RPC[/green]")

    last_payload = None
    paused_since: float | None = None
    long_pause = False

    # Initial small randomized delay to avoid stampeding herd when many clients start
    time.sleep(random.uniform(0, min(2.0, interval)))

    while True:
        data = get_presence(jellyfin_url, api_key)
        if not data:
            time.sleep(interval + random.uniform(0, 0.5 * max(0.1, interval)))
            continue

        if not data.get("active"):
            if last_payload is not None:
                try:
                    rpc.clear()
                except Exception:
                    pass
                last_payload = None
            paused_since = None
            long_pause = False
            time.sleep(interval + random.uniform(0, 0.5 * max(0.1, interval)))
            continue

        payload = {
            "details": data.get("details") or None,
            "state": data.get("state") or None,
            "large_image": data.get("large_image") or None,
            "large_text": data.get("large_text") or None,
            "small_image": data.get("small_image") or None,
            "small_text": data.get("small_text") or None,
            "start": int(data.get("start_timestamp")) if data.get("start_timestamp") else None,
        }

        payload = {k: v for k, v in payload.items() if v is not None}

        # Handle paused timing and long-pause clearing
        if data.get("is_paused"):
            if paused_since is None:
                paused_since = time.time()
            paused_elapsed = time.time() - paused_since
            if paused_elapsed >= 180:  # 3 minutes
                if last_payload is not None:
                    try:
                        rpc.clear()
                    except Exception:
                        pass
                    last_payload = None
                long_pause = True
            else:
                long_pause = False
        else:
            paused_since = None
            long_pause = False

        if not long_pause and payload != last_payload:
            try:
                rpc.update(**payload)
                last_payload = payload
            except Exception as e:
                console.print(f"[red]Failed to update RPC: {e}[/red]")

        # Backoff when paused; normal faster polling when playing
        if data.get("is_paused"):
            pause_delay = random.uniform(30.0, 45.0)
            time.sleep(pause_delay)
        else:
            time.sleep(interval + random.uniform(0, 0.5 * max(0.1, interval)))


if __name__ == "__main__":
    main()


