#!/usr/bin/env python3

import os
import sys


def read_environ(pid: str) -> dict[str, str]:
    with open(f"/proc/{pid}/environ", "rb") as f:
        data = f.read()

    env = {}
    for entry in data.split(b"\0"):
        if not entry:
            continue
        key, sep, value = entry.partition(b"=")
        if sep:
            env[key.decode(errors="replace")] = value.decode(errors="replace")
    return env


def read_cmdline(pid: str) -> str:
    with open(f"/proc/{pid}/cmdline", "rb") as f:
        data = f.read()
    parts = [p.decode(errors="replace") for p in data.split(b"\0") if p]
    return " ".join(parts)


def main() -> int:
    if len(sys.argv) != 2:
        print(f"usage: {sys.argv[0]} <pid>", file=sys.stderr)
        return 1

    pid = sys.argv[1]

    try:
        cmd = read_cmdline(pid)
        proc_env = read_environ(pid)
    except FileNotFoundError:
        print(f"process {pid} not found", file=sys.stderr)
        return 1
    except PermissionError:
        print(f"permission denied reading /proc/{pid}", file=sys.stderr)
        return 1

    # cur_env = dict(os.environ)
    cur_env = read_environ("18548")

    added = {}
    changed = {}

    for key, value in proc_env.items():
        if key not in cur_env:
            added[key] = value
        elif cur_env[key] != value:
            changed[key] = (cur_env[key], value)

    print("CMD:")
    print(cmd if cmd else "(empty)")

    print("\nADDED:")
    if added:
        for key in sorted(added):
            print(f"{key}={added[key]}")
    else:
        print("(none)")

    print("\nCHANGED:")
    if changed:
        for key in sorted(changed):
            old, new = changed[key]
            print(key)
            print(f"  current: {old}")
            print(f"  process: {new}")
    else:
        print("(none)")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
