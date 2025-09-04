import json
import os
import sys
import time
from pathlib import Path

import requests
try:
    from pypresence import Presence  # type: ignore
except Exception:  # pragma: no cover
    Presence = None  # fallback when pypresence is not installed


def _config_paths():
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
            with p.open("r", encoding="utf-8") as f:
                return json.load(f)
    # env fallback
    return {
        "jellyfin_url": os.environ.get("JELLYFIN_URL"),
        "api_key": os.environ.get("JELLYFIN_API_KEY"),
        "discord_client_id": os.environ.get("DISCORD_CLIENT_ID", "1199810830972170261"),
        "interval": float(os.environ.get("POLL_INTERVAL", "5")),
        "include_token_in_image_url": os.environ.get("INCLUDE_TOKEN_IN_IMAGE_URL", "false").lower() == "true",
    }


def get_presence(base_url: str, api_key: str) -> dict | None:
    headers = {
        "X-Emby-Token": api_key,
        "Accept": "application/json",
        "User-Agent": "Jellyfin-Discord-RPC-CLI-Test/1.0",
    }
    url = base_url.rstrip("/") + "/Plugins/DiscordRpc/Presence/Me"
    try:
        r = requests.get(url, headers=headers, timeout=10)
        if r.status_code == 401:
            print("Unauthorized. Check your API key.")
            return None
        r.raise_for_status()
        return r.json()
    except Exception as e:
        print(f"Error fetching presence: {e}")
        return None


def build_preview_payload(cfg: dict, data: dict) -> dict:
    payload = {
        "details": data.get("details") or None,
        "state": data.get("state") or None,
        "large_image": data.get("large_image") or None,
        "large_text": data.get("large_text") or None,
        "small_image": data.get("small_image") or None,
        "small_text": data.get("small_text") or None,
        "start": int(data.get("start_timestamp")) if data.get("start_timestamp") else None,
        "end": int(data.get("end_timestamp")) if data.get("end_timestamp") else None,
    }
    # prefer cover url when provided
    cover_path = data.get("cover_image_path")
    if cover_path and cfg.get("jellyfin_url"):
        base = cfg["jellyfin_url"].rstrip("/")
        url = base + "/" + cover_path.lstrip("/")
        if cfg.get("include_token_in_image_url") and cfg.get("api_key"):
            sep = "&" if ("?" in url) else "?"
            url = f"{url}{sep}X-Emby-Token={cfg['api_key']}"
        payload["large_image"] = url
    # strip None
    return {k: v for k, v in payload.items() if v is not None}


def connect_rpc(client_id: str):
    if Presence is None:
        print("pypresence is not installed. Run: pip install -r requirements.txt")
        return None
    try:
        rpc = Presence(client_id)
        rpc.connect()
        print(f"Connected to Discord RPC with client_id {client_id}")
        return rpc
    except Exception as e:
        print(f"Failed to connect to Discord RPC: {e}")
        return None


def send_test_presence(rpc) -> None:
    if rpc is None:
        return
    try:
        start = int(time.time()) - 30
        end = start + 300
        rpc.update(
            details="Watching: Example Movie",
            state="Drama, Mystery • 04:30 left",
            large_image="jellyfin",
            large_text="Test",
            start=start,
            end=end,
        )
        print("Sent test presence to Discord with progress bar. Showing for ~10 seconds...")
        time.sleep(10)
        rpc.clear()
        print("Cleared test presence.")
    except Exception as e:
        print(f"Failed to send test presence: {e}")


def hold_presence(rpc, payload: dict) -> None:
    if rpc is None:
        return
    try:
        rpc.update(**payload)
        print("Presence set. Press Ctrl+C to clear and exit.")
        base_state = payload.get("state") or ""
        start = payload.get("start")
        end = payload.get("end")
        while True:
            time.sleep(20)
            if start and end:
                left = max(0, int(end) - int(time.time()))
                t = time.strftime("%M:%S", time.gmtime(left)) if left < 3600 else time.strftime("%H:%M:%S", time.gmtime(left))
                # Replace existing trailing time-left if present; otherwise append
                new_state = base_state
                if " left" in base_state:
                    if "•" in base_state:
                        new_state = base_state.rsplit("•", 1)[0].strip()
                    else:
                        new_state = base_state.rsplit(" ", 1)[0].strip()
                new_state = f"{new_state} • {t} left" if new_state else f"{t} left"
                try:
                    rpc.update(state=new_state)
                    base_state = new_state
                except Exception:
                    pass
    except KeyboardInterrupt:
        try:
            rpc.clear()
            print("Cleared presence.")
        except Exception:
            pass
    except Exception as e:
        print(f"Failed to hold presence: {e}")
    


def main():
    cfg = load_config()
    print("Offline mode: Simulating presence without Jellyfin...")

    now = int(time.time())
    def fmt_left(start_ts: int, end_ts: int) -> str:
        left = max(0, end_ts - int(time.time()))
        ts = time.strftime("%M:%S", time.gmtime(left)) if left < 3600 else time.strftime("%H:%M:%S", time.gmtime(left))
        return f"{ts} left"

    simulated = [
        {
            "active": True,
            "details": "Watching: The Batman",
            "state": f"Crime, Mystery, Thriller • {fmt_left(now - 1800, now + 1800)}",
            "large_image": "jellyfin",
            "large_text": "Jellyfin",
            "small_image": "play",
            "small_text": "Playing",
            "start_timestamp": now - 1800,
            "end_timestamp": now + 1800,
            "is_paused": False,
            "item_id": "00000000000000000000000000000001",
            "item_type": "Movie",
            "cover_image_path": "Items/00000000000000000000000000000001/Images/Primary?tag=d41d8cd98f"
        },
        {
            "active": True,
            "details": "Watching: Example Show",
            "state": f"Example Show S01E03 • Drama, Sci-Fi • {fmt_left(now - 600, now + 1200)}",
            "large_image": "jellyfin",
            "large_text": "Episode title",
            "small_image": "pause",
            "small_text": "Paused",
            "start_timestamp": now - 600,
            "end_timestamp": now + 1200,
            "is_paused": True,
            "item_id": "00000000000000000000000000000002",
            "item_type": "Episode",
            "cover_image_path": "Items/00000000000000000000000000000002/Images/Primary?tag=0cc175b9c0"
        },
        {
            "active": False
        }
    ]

    # Always use the hardcoded Client ID for testing and prod
    client_id = "1413211075222048879"
    rpc = connect_rpc(client_id)
    send_test_presence(rpc)

    # Use the first simulated active scenario and hold it
    data = simulated[0]
    print("\nOffline scenario - Presence JSON:")
    print(json.dumps(data, indent=2))
    preview = build_preview_payload(cfg, data)
    # Ensure offline test doesn't rely on Discord asset keys
    preview.pop("large_image", None)
    preview.pop("small_image", None)
    print("Preview RPC payload:")
    print(json.dumps(preview, indent=2))
    hold_presence(rpc, preview)


if __name__ == "__main__":
    main()


