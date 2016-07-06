#!/usr/bin/env python
import subprocess, signal, time, sys, os
sys.path.append(os.path.join(os.getcwd(), "External"))

import External.mechanize

proc = None
def killProc():
	global proc
	if (proc is not None) and (proc.poll() is None):
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
		try:
			killProc()
			proc = subprocess.Popen(["python", "Main.py", "-service"])
		except Exception as e:
			time.sleep(60) # wait a minute

		while (proc is not None) and (proc.poll() is None):
			for i in range(0, 60): # wait a minute
				time.sleep(1)
				if (proc is None) or (proc.poll() is not None):
					break
			
			if (proc is None) or (proc.poll() is not None):
				break
			
			# open page
			try:
				br = External.mechanize.Browser()
				br.open("http://localhost:5000")
			except Exception as e:
				print "Restart My Home: cannot open the web page"
				killProc()
except:
	killProc()