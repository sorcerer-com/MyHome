#!/usr/bin/env python
import logging
import secrets
import sys
from datetime import timedelta

from flask import Flask, abort, request

from Config import Config
from MyHome import MyHome
from Utils import Utils
from Views import views

debugLevel = logging.INFO if "debug" not in sys.argv else logging.DEBUG
Utils.setupLogging(Config.LogFilePath, debugLevel)
logger = logging.getLogger("main")


app = Flask(__name__, template_folder="UI", static_folder="UI/static")
app.register_blueprint(views)

myHome = MyHome()


@app.template_filter()
def typeDictSort(value):
    keys = sorted(value.keys(), key=lambda x: type(x).__name__)
    return [(key, value[key]) for key in keys]


@app.template_filter()
def gettype(value):
    return type(value).__name__


@app.template_filter()
def toString(value):
    return Utils.string(value)


@app.route("/sensor/data", methods=["POST"])
def sensor():
    # curl "http://127.0.0.1:5000/sensor/data" -i -X POST -H "token: c9dd348f9b48020e5d0a7204d5ce6eb8" -H "Content-Type: application/json" -d '[{"name": "test", "value": '$((1 + RANDOM % 100))'}]'
    if "token" not in request.headers or not request.is_json or not isinstance(request.get_json(), list):
        abort(404)

    system = myHome.systems["SensorsSystem"]
    if not system.processData(request.headers["token"], request.get_json()):
        abort(404)
    return ""


if __name__ == "__main__":
    if myHome.config.appSecret == "":
        myHome.config.appSecret = secrets.token_hex(24)

    app.secret_key = myHome.config.appSecret
    app.permanent_session_lifetime = timedelta(minutes=15)
    app.config['TEMPLATES_AUTO_RELOAD'] = "debug" in sys.argv # in debug mode
    app.run(debug=False, host="0.0.0.0", threaded=True)
    myHome.stop()
    logger.info("")
