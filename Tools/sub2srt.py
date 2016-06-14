#!/usr/bin/env
import sys, os, codecs
from datetime import *


def convert(filePath, fps):
	newFilePath = filePath[:-4] + ".srt"
	with codecs.open(filePath, "r", encoding="windows-1251") as f1:
		with codecs.open(newFilePath, "w", encoding="utf8") as f2:
			count = 1
			for line in f1:
				#line = unicode(line, "windows-1251").encode("utf-8")
				spilts = line.strip().replace("{", "").split("}")
				f2.write(str(count) + "\n") # number
				time = datetime(1900, 1, 1) + timedelta(seconds= float(spilts[0]) / fps )
				strTime = "%02d:%02d:%02d,%03d" % (time.hour, time.minute, time.second, time.microsecond / 1000)
				f2.write(strTime) # start time
				f2.write(" --> ")
				time = datetime(1900, 1, 1) + timedelta(seconds= float(spilts[1]) / fps )
				strTime = "%02d:%02d:%02d,%03d" % (time.hour, time.minute, time.second, time.microsecond / 1000)
				f2.write(strTime) # end time
				f2.write("\n")
				for i in range(2, len(spilts)):
					f2.write(spilts[i].replace("|", "\n") + " ") # content
				f2.write("\n")
				f2.write("\n")
				count += 1
				
def fixEncoding(filePath):
	content = ""
	with codecs.open(filePath, "r", encoding="windows-1251") as f1:
		content = f1.read()
	with codecs.open(filePath, "w", encoding="utf8") as f2:
		f2.write(content)
		
		
if len(sys.argv) < 3:
	print "Usage: fps filePath"
else:
	fps = float(sys.argv[1])
	path = sys.argv[2]
	if os.path.isfile(path) and path.endswith(".sub"):
		convert(path, fps)
	elif os.path.isfile(path) and path.endswith(".srt"):
		fixEncoding(path)
	elif os.path.isdir(path):
		for file in os.listdir(path):
			try:
				if file.endswith(".sub"):
					print file
					convert(path + file, fps)
				elif file.endswith(".srt"):
					print file
					fixEncoding(path + file)
			except Exception as e:
				print "\t" + e
	else:
		print "invalid path"