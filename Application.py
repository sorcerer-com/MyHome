#!/usr/bin/python
import os
from Tkinter import *
from MyHome import *

class Application(Frame):
	def __init__(self, master = None):
		Frame.__init__(self, master)
		
		self.myHome = MHome()
		self.pack()
		self.initDesign()
				
	def initDesign(self):
		# systems buttons
		self.systemsLabelFrame = LabelFrame(self, text="Systems")
		self.systemsLabelFrame.pack()
		self.systemsButtons = {}
		for (key, value) in self.myHome.systems.items():
			button = Button(self.systemsLabelFrame)
			button["text"] = key + " " + ("On" if value.enabled else "Off")
			button["command"] = lambda: self.systemButtonClick(button)
			button.system = value
			button.pack(padx=5, pady=5)
			self.systemsButtons[key] = button
		
	def systemButtonClick(self, button):
		button.system.enabled = not button.system.enabled
		self.systemsButtons[button.system.Name]["text"] = button.system.Name + " " + ("On" if button.system.enabled else "Off")
		
		
os.chdir("bin")
root = Tk()
app = Application(master = root)
app.master.title("My Home")
app.master.minsize(640, 480)
app.mainloop()
app.myHome.__del__()