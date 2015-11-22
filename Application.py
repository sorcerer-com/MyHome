#!/usr/bin/python
from Tkinter import *
from MyHome import *

class Application(Frame):
	def __init__(self, master = None):
		Frame.__init__(self, master)
		
		self.MyHome = MHome()
		self.pack()
		self.initDesign()
				
	def initDesign(self):
		# systems buttons
		self.SystemsLabelFrame = LabelFrame(self, text="Systems")
		self.SystemsLabelFrame.pack()
		self.SystemsButtons = {}
		for (key, value) in self.MyHome.Systems.items():
			button = Button(self.SystemsLabelFrame)
			button["text"] = key + " " + ("On" if value.Enabled else "Off")
			button["command"] = lambda: self.systemButtonClick(button)
			button.system = value
			button.pack(padx=5, pady=5)
			self.SystemsButtons[key] = button
			
		# sensors
		self.SensorsLabelFrame = LabelFrame(self, text="Sensors")
		self.SensorsLabelFrame.pack()
		self.SensorsTexts = {}
		for (key, value) in self.MyHome.Sensors.items():
			text = Label(self.SensorsLabelFrame)
			text["text"] = key + ": " + value.info()
			text.pack(padx=5, pady=5)
			self.SensorsTexts[key] = text
		
	def systemButtonClick(self, button):
		# TODO: may be add log for enabling
		button.system.Enabled = not button.system.Enabled
		self.SystemsButtons[button.system.Name]["text"] = button.system.Name + " " + ("On" if button.system.Enabled else "Off")
		
		
root = Tk()
app = Application(master = root)
app.master.title("My Home")
app.master.minsize(640, 480)
app.mainloop()
app.MyHome.__del__()