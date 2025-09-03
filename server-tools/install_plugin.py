import os
import shutil
import subprocess
import sys
from pathlib import Path
from typing import List, Optional


def find_repo_root(start: Path) -> Path:
    current = start.resolve()
    for _ in range(6):
        if (current / "jellyfin-discord-rpc-plugin" / "DiscordRpcPlugin.csproj").exists():
            return current
        if current.parent == current:
            break
        current = current.parent
    raise FileNotFoundError(
        "Could not locate 'jellyfin-discord-rpc-plugin/DiscordRpcPlugin.csproj' relative to this script."
    )


def build_plugin(plugin_dir: Path) -> Path:
    publish_dir = plugin_dir / "bin" / "installer_publish"
    publish_dir.mkdir(parents=True, exist_ok=True)
    cmd = [
        "dotnet",
        "publish",
        "-c",
        "Release",
        "-o",
        str(publish_dir),
    ]
    subprocess.run(cmd, cwd=str(plugin_dir), check=True)
    return publish_dir


def candidate_plugin_dirs() -> List[Path]:
    candidates: List[Path] = []

    # Env override
    env_override = os.environ.get("JELLYFIN_PLUGIN_DIR")
    if env_override:
        candidates.append(Path(env_override))

    # Linux common locations
    candidates.extend(
        [
            Path("/var/lib/jellyfin/plugins"),
            Path("/var/lib/jellyfin/data/plugins"),
            Path.home() / ".local/share/jellyfin/plugins",
        ]
    )

    # Windows
    program_data = os.environ.get("ProgramData")
    if program_data:
        candidates.append(Path(program_data) / "Jellyfin" / "Server" / "plugins")
    appdata = os.environ.get("APPDATA")
    if appdata:
        candidates.append(Path(appdata) / "Jellyfin" / "Server" / "plugins")

    # macOS (best guess)
    candidates.append(Path.home() / "Library/Application Support/Jellyfin/Server/plugins")

    # Remove duplicates while preserving order
    seen = set()
    unique: List[Path] = []
    for c in candidates:
        p = c.resolve()
        if p not in seen:
            unique.append(p)
            seen.add(p)
    return unique


def choose_plugin_dir() -> Path:
    for p in candidate_plugin_dirs():
        try:
            p.mkdir(parents=True, exist_ok=True)
            return p
        except Exception:
            continue
    raise PermissionError(
        "Failed to create a Jellyfin plugins directory. Set JELLYFIN_PLUGIN_DIR to override."
    )


def install_publish(publish_dir: Path, plugins_root: Path) -> Path:
    target_dir = plugins_root / "DiscordRpc"
    if target_dir.exists():
        try:
            shutil.rmtree(target_dir)
        except Exception:
            # Fallback to removing files inside
            for child in target_dir.glob("**/*"):
                try:
                    if child.is_file() or child.is_symlink():
                        child.unlink()
                except Exception:
                    pass
    target_dir.mkdir(parents=True, exist_ok=True)

    for item in publish_dir.iterdir():
        dest = target_dir / item.name
        if item.is_dir():
            shutil.copytree(item, dest, dirs_exist_ok=True)
        else:
            shutil.copy2(item, dest)

    return target_dir


def try_restart_service() -> None:
    # Linux systemd
    systemctl = shutil.which("systemctl")
    if systemctl:
        try:
            subprocess.run([systemctl, "restart", "jellyfin"], check=True)
            print("Restarted Jellyfin via systemd")
            return
        except Exception:
            pass

    # Windows Service Control
    if os.name == "nt":
        sc = shutil.which("sc")
        service_names = ["Jellyfin", "jellyfin", "JellyfinServer"]
        if sc:
            for name in service_names:
                try:
                    subprocess.run([sc, "stop", name], check=False)
                    subprocess.run([sc, "start", name], check=False)
                    print(f"Attempted to restart Windows service: {name}")
                    return
                except Exception:
                    continue

    print(
        "Could not automatically restart Jellyfin. Please restart the Jellyfin service manually."
    )


def main() -> None:
    try:
        repo_root = find_repo_root(Path(__file__).parent)
    except FileNotFoundError as e:
        print(f"Error: {e}")
        sys.exit(1)

    plugin_dir = repo_root / "jellyfin-discord-rpc-plugin"

    # Ensure dotnet is available
    if not shutil.which("dotnet"):
        print(
            "Error: dotnet SDK not found. Install .NET 8 SDK from https://dotnet.microsoft.com/ and rerun."
        )
        sys.exit(1)

    try:
        publish_dir = build_plugin(plugin_dir)
    except subprocess.CalledProcessError as e:
        print(f"dotnet publish failed with code {e.returncode}")
        sys.exit(e.returncode)

    try:
        plugins_root = choose_plugin_dir()
    except PermissionError as e:
        print(f"Error: {e}")
        sys.exit(1)

    target = install_publish(publish_dir, plugins_root)
    print(f"Installed plugin to: {target}")

    try_restart_service()
    print("Done.")


if __name__ == "__main__":
    main()


