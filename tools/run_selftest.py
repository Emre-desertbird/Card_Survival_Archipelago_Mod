"""Play a scripted session in the real game against a real local Archipelago server.

1. Generates a solo seed with the working APWorld (tests/fixtures).
2. Starts that seed on a local Archipelago server (MultiServer.py from --ap-src).
3. Starts the modded dev copy with CSAP_SELFTEST: the plugin connects, starts a new game,
   discovers and researches blueprints, receives items, saves, reloads, loads the checkpoint
   and triggers an ending, then writes a report and quits (game_plugin/SelfTest.cs).
4. Checks that the player's normal saves were not touched.

Test saves go to "...(Archipelago Test)", never to the normal or the dev saves.
Usage: run_selftest.py --ap-src <Archipelago source> [--python <python for Archipelago>]
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import socket
import subprocess
import sys
import time
from pathlib import Path

from common import DEV_GAME, GAME_EXE, LOCAL, LOCAL_LOW, PROJECT, WORLD, save_fingerprint

TEST_SAVES = LOCAL_LOW / "Card Survival - Tropical Island (Archipelago Test)"


def free_port() -> int:
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        return probe.getsockname()[1]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--ap-src", type=Path, default=os.environ.get("AP_SRC"), required="AP_SRC" not in os.environ)
    parser.add_argument("--python", default=sys.executable, help="Python that runs Archipelago (3.11-3.13)")
    parser.add_argument("--seed", default="7")
    parser.add_argument("--timeout", type=int, default=600)
    args = parser.parse_args()

    work = LOCAL / "selftest"
    shutil.rmtree(work, ignore_errors=True)
    (work / "output").mkdir(parents=True)
    source = Path(args.ap_src).resolve(strict=True)

    # 1. Generate.
    target = source / "worlds" / WORLD.name
    shutil.rmtree(target, ignore_errors=True)
    shutil.copytree(WORLD, target, ignore=shutil.ignore_patterns("__pycache__"))
    try:
        subprocess.run([args.python, "-B", "Generate.py", "--seed", args.seed, "--player_files_path",
                        str(PROJECT / "tests" / "fixtures"), "--outputpath", str(work / "output")],
                       cwd=source, env=dict(os.environ, SKIP_REQUIREMENTS_UPDATE="1"), check=True,
                       stdout=subprocess.DEVNULL)
        archive = next((work / "output").glob("AP_*.zip"))

        # 2. Serve it.
        port = free_port()
        server_log = (work / "server.log").open("w", encoding="utf-8")
        server = subprocess.Popen([args.python, "-B", "MultiServer.py", "--host", "127.0.0.1", "--port", str(port),
                                   "--loglevel", "info", str(archive)],
                                  cwd=source, env=dict(os.environ, SKIP_REQUIREMENTS_UPDATE="1"),
                                  stdin=subprocess.PIPE, stdout=server_log, stderr=subprocess.STDOUT)
        try:
            deadline = time.time() + 60
            while time.time() < deadline:
                with socket.socket() as probe:
                    if probe.connect_ex(("127.0.0.1", port)) == 0:
                        break
                time.sleep(0.5)
            else:
                raise SystemExit("The Archipelago server did not start; see " + str(work / "server.log"))

            # 3. Play.
            shutil.rmtree(TEST_SAVES, ignore_errors=True)
            report = work / "report.json"
            config = work / "selftest.json"
            config.write_text(json.dumps({"host": f"127.0.0.1:{port}", "slot": "CSTI_Test",
                                          "report": str(report)}), encoding="utf-8")
            before = save_fingerprint()
            game = subprocess.Popen([str(DEV_GAME / GAME_EXE), "-logFile", str(work / "Player.log"),
                                     "-screen-fullscreen", "0", "-screen-width", "1280", "-screen-height", "720"],
                                    cwd=DEV_GAME, env=dict(os.environ, CSAP_SELFTEST=str(config), CSAP_TEST_SAVES="1"))
            try:
                game.wait(timeout=args.timeout)
            except subprocess.TimeoutExpired:
                game.kill()
                print("The game did not finish in time.")
            if save_fingerprint() != before:
                raise SystemExit("STOP: the normal save files changed during this run. Save isolation is broken.")
        finally:
            server.kill()
            server_log.close()
    finally:
        shutil.rmtree(target, ignore_errors=True)

    if not report.is_file():
        raise SystemExit("No report was written; see " + str(DEV_GAME / "BepInEx" / "LogOutput.log"))
    result = json.loads(report.read_text(encoding="utf-8"))
    for step in result["steps"]:
        print(("OK   " if step["ok"] else "FAIL ") + step["step"] + ("  " + json.dumps(step["detail"]) if "detail" in step else ""))
    goal = "has completed their goal" in (work / "server.log").read_text(encoding="utf-8", errors="replace")
    print("server saw the goal:", goal)
    print("normal saves untouched: True")
    raise SystemExit(0 if result["passed"] and goal else 1)


if __name__ == "__main__":
    main()
