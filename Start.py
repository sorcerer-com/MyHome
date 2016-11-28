#!/usr/bin/env python
import subprocess, signal, time, sys, os
sys.path.append(os.path.join(os.getcwd(), "External"))

import External.mechanize

proc = None
def killProc():
	global proc
	if (proc is not None) and (proc.poll() is None):
		proc.terminate()
		# check one second for exit
		for i in range(0, 10):
			time.sleep(0.1)
			if proc.poll() is not None:
				break
		
		if proc.poll() is None:
			proc.kill()
			time.sleep(1)
		proc = None

def signal_handler(signal, frame):
	global proc
	killProc()
	sys.exit(0)
signal.signal(signal.SIGTERM, signal_handler)
		
while True:
	try:
		killProc()
		proc = subprocess.Popen(["python", "Main.py", "-service"])

		while (proc is not None) and (proc.poll() is None):
			for i in range(0, 12): # wait a minute
				time.sleep(5)
				if (proc is None) or (proc.poll() is not None):
					break
			
			if (proc is None) or (proc.poll() is not None):
				break
			
			# open page
			try:
				br = External.mechanize.Browser()
				br.open("http://localhost:5000", timeout=30)
			except Exception as e:
				print "Error: Cannot open the web page restart My Home"
				break
		print ""
	except (KeyboardInterrupt, SystemExit) as e:
		break
	except:
		time.sleep(60) # wait a minute
		pass
