#!/usr/bin/env python
import subprocess, signal, time, sys, os
sys.path.append(os.path.join(os.getcwd(), "External"))

import External.mechanize

proc = None
def killProc():
	global proc
	if proc is not None:
		proc.terminate()
		proc.kill()
		proc = None

def signal_handler(signal, frame):
	global proc
	killProc()
	sys.exit(0)
signal.signal(signal.SIGTERM, signal_handler)
		
try:
	while True:
		proc = subprocess.Popen(["python", "Main.py", "-service"])

		while (proc is not None) and (proc.poll() is None):
			time.sleep(60) # wait a minute

			# open page
			try:
				br = External.mechanize.Browser()
				br.open("http://localhost:5000")
			except Exception as e:
				print "Restart My Home: cannot open the web page"
				killProc()
except:
	killProc()