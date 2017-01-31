#!/usr/bin/env python
import subprocess, signal, time, sys, os
sys.path.append(os.path.join(os.getcwd(), "External"))

import External.mechanize

proc = None
def killProc():
	global proc
	if (proc is not None) and (proc.poll() is None):
		# send interrupt signal
		proc.send_signal(signal.SIGINT)
		# check one second for exit
		for i in range(0, 20):
			time.sleep(0.1)
			if i == 10: # if process isn't closed in half of the time, call terminate
				proc.terminate()
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
signal.signal(signal.SIGINT, signal_handler)
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
			
			# try open page 3 times
			kill = True
			for i in range(0, 3):
				try:
					br = External.mechanize.Browser()
					br.set_handle_robots(False)
					br.open("http://localhost:5000", timeout=30)
					kill = False
					break
				except Exception as e:
					print e
					time.sleep(10)
			if kill:
				print "Error: Cannot open the web page restart My Home"
				break
		print ""
	except (KeyboardInterrupt, SystemExit) as e:
		killProc()
		break
	except:
		time.sleep(60) # wait a minute
		pass
