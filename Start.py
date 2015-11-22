#!/usr/bin/python3.7
# pylint: disable=global-statement
import logging
import os
import signal
import subprocess
import sys
import time
from datetime import datetime, timedelta

import robobrowser

from Utils import Utils

Utils.setupLogging("bin/starter.log",
                   logLevel=logging.DEBUG,
                   showInConsole=False,
                   useBufferHandler=False)
logger = logging.getLogger()

proc = None


def killProc():
    global proc
    if (proc is not None) and (proc.poll() is None):
        time.sleep(0.5)
        # send interrupt signal
        if sys.platform != "win32":
            proc.send_signal(signal.SIGINT)
        # check one second for exit
        waitSeconds = 2
        for j in range(0, waitSeconds * 10):
            time.sleep(0.1)
            if j == waitSeconds * 10 / 2:  # if process isn't closed in half of the time, call terminate
                proc.terminate()
            if proc.poll() is not None:
                break

        if proc.poll() is None:
            proc.kill()
            time.sleep(1)
        proc = None
        time.sleep(0.5)


def signal_handler(_, __):
    global proc
    killProc()
    sys.exit(0)


# args - Start.py "command" "web address"
if len(sys.argv) < 3:
    sys.argv.append("python3 Main.py")
    if os.path.isdir("venv"):
        sys.argv[1] = "venv/bin/" + sys.argv[1]
    sys.argv.append("http://localhost:5000")

signal.signal(signal.SIGINT, signal_handler)
signal.signal(signal.SIGTERM, signal_handler)

lastRestartTime = datetime.now()
restartCount = 0
while True:
    # if restart too often wait more
    if datetime.now() - lastRestartTime > timedelta(seconds=10):
        restartCount = 0
    else:
        restartCount += 1
        if restartCount > 5:
            time.sleep(60)  # wait a minute
    lastRestartTime = datetime.now()

    try:
        killProc()
        proc = subprocess.Popen(sys.argv[1].split())

        while (proc is not None) and (proc.poll() is None):
            for i in range(0, 12):  # wait a minute
                time.sleep(5)
                if (proc is None) or (proc.poll() is not None):
                    break

            if (proc is None) or (proc.poll() is not None):
                logger.error("Process didn't start, so try to restart it")
                break

            # try open page 3 times
            kill = True
            for i in range(0, 3):
                try:
                    br = robobrowser.RoboBrowser(timeout=30)
                    br.open(sys.argv[2])
                    kill = False
                    break
                except Exception as e:
                    logger.debug(str(e))
                    time.sleep(30)
            if kill:
                logger.error("Cannot open the web page try to restart")
                break
        logger.info("")
    except (KeyboardInterrupt, SystemExit) as e:
        killProc()
        break
    except Exception as e:
        logger.debug(str(e))
        time.sleep(60)  # wait a minute
