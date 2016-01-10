import subprocess
from Utils.Logger import *
from External.pymouse import PyMouse
from External.pykeyboard import PyKeyboard

class PCControlService:
    Mouse = PyMouse()
    Keyboard = PyKeyboard()
    
    @staticmethod
    def openBrowser(url):
        Logger.log("info", "PCControlService: open browser with url '%s'" % url)
        subprocess.call(["sensible-browser", url], stdout=subprocess.PIPE, stderr=subprocess.PIPE)