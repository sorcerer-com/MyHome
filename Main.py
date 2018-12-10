#!/usr/bin/env python
import logging
import os
import secrets
from datetime import datetime, timedelta

from flask import Flask

from Config import Config
from MyHome import MyHome
from Utils import Utils
from Views import views

Utils.setupLogging(Config.LogFilePath)
logger = logging.getLogger("main")


app = Flask(__name__, template_folder="UI", static_folder="UI/static")
app.register_blueprint(views)

myHome = MyHome()

if __name__ == "__main__":
    if myHome.config.appSecret == "":
        myHome.config.appSecret = secrets.token_hex(24)

    app.secret_key = myHome.config.appSecret
    app.permanent_session_lifetime = timedelta(minutes=15)
    app.config['TEMPLATES_AUTO_RELOAD'] = True  # TODO: if debug
    app.run(debug=False, host="0.0.0.0", threaded=True)
    myHome.stop()
