import json
import logging
import logging.handlers
import re
from datetime import datetime, timedelta
from typing import get_type_hints

from Utils.Decorators import try_catch, type_check
from Utils.LoggingFilter import LoggingFilter


@type_check
def setupLogging(fileName: str, fileLogLevel: int = logging.INFO, showInConsole: bool = True, useBufferHandler: bool = True) -> None:
    """ Setup Logging module.

    Arguments:
            fileName {str} -- Path to the file which will contains the logs.

    Keyword Arguments:
            fileLogLevel {int} -- Log level of the file handler. (default: {logging.INFO})
            showInConsole {bool} -- Indicate whether the logs will be shown in the standart output. (default: {True})
            useBufferHandler {bool} -- Indicate whether the logs will be collected in the buffer. (default: {True})
    """

    logger = logging.getLogger()
    logger.setLevel(logging.DEBUG)
    formatter = logging.Formatter(
        "%(asctime)-20s %(name)-12s %(levelname)-8s %(message)s", "%d/%m/%Y %H:%M:%S")

    # add RotatingFileHandler
    file = logging.handlers.RotatingFileHandler(
        fileName, maxBytes=1024*1024, backupCount=3)
    file.setLevel(fileLogLevel)
    file.setFormatter(formatter)
    logger.addHandler(file)

    noWerkzeugInfoFilter = LoggingFilter(lambda r: not (
        r.name == "werkzeug" and r.levelno == logging.INFO))
    if showInConsole:
        # add Console handler
        console = logging.StreamHandler()
        console.setLevel(logging.INFO)
        console.setFormatter(formatter)
        console.addFilter(noWerkzeugInfoFilter)
        logger.addHandler(console)

    if useBufferHandler:
        # add BufferHandler
        buffer = logging.handlers.BufferingHandler(500)
        buffer.setLevel(logging.INFO)
        buffer.setFormatter(formatter)
        buffer.addFilter(noWerkzeugInfoFilter)
        logger.addHandler(buffer)


@type_check
def getLogs() -> list:
    """ Return list of the last 500 log records.

    Returns:
            list -- List of the last 500 log records.
    """

    logger = logging.getLogger()
    handlers = list(h for h in logger.handlers if isinstance(
        h, logging.handlers.BufferingHandler))  # get all BufferingHandlers
    if len(handlers) > 0:
        return [handlers[0].formatter.format(rec) for rec in handlers[0].buffer]
    return None


@type_check
def getFields(obj: object, publicOnly: bool = True, includeStatic: bool = False) -> dict:
    """ Return dictionary with fields names and values of the set object.

    Arguments:
            obj {object} -- Object from which the fields will be get.

    Keyword Arguments:
            publicOnly {bool} -- Indicate whether only public fields will be included. (default: {True})
            includeStatic {bool} -- Indicate whether static fields will be included. (default: {False})

    Returns:
            dict -- Dictionary with fields names and values of the set object.
    """

    result = {}
    items = dir(obj)
    for attr in items:
        value = getattr(obj, attr)
        if publicOnly and attr.startswith("_"):
            continue
        if not includeStatic and hasattr(type(obj), attr):
            staticValue = getattr(type(obj), attr)
            # skip only non properties or properties without getter or setter
            if type(staticValue) is not property or \
               staticValue.fget == None or \
               staticValue.fset == None:
                continue
        if not callable(value):
            result[attr] = value
    return result


@try_catch("Cannot parse value")
@type_check
def string(value: object) -> str:
    """ Convert to string the set value.

    Arguments:
        value {object} -- Value which will be converted to string.

    Returns:
        str -- String representation of the value.
    """

    valueType = type(value)
    if valueType is datetime:
        return value.strftime("%Y-%m-%d %H:%M:%S")
    elif valueType is timedelta:
        value = datetime(1900, 1, 1) + value
        return "%02d-%02d-%02d %02d:%02d:%02d" % (value.year - 1900, value.month - 1, value.day - 1, value.hour, value.minute, value.second)
    elif valueType is list:
        temp = [(type(v).__name__, string(v)) for v in value]
        return json.dumps(temp)
    elif valueType is dict:
        temp = {k: (type(k).__name__, type(v).__name__, string(v))
                for k, v in value.items()}
        return json.dumps(temp)
    return str(value)


@try_catch("Cannot convert value to string")
@type_check
def parse(value: str, valueType: type) -> object:
    """ Parse string value to the set type.

    Arguments:
            value {str} -- String which will be parsed.
            valueType {type} -- Type to which the string will be parsed.

    Raises:
            Exception -- It's raised if try to parse to unsupported type.

    Returns:
            object -- Parsed value.
    """

    if valueType is bool:
        return value == "True"
    elif valueType is int:
        return int(value)
    elif valueType is float:
        return float(value)
    elif valueType is str:
        return value
    elif valueType is datetime:
        return datetime.strptime(value, "%Y-%m-%d %H:%M:%S")
    elif valueType is timedelta:
        value = re.split("-| |:", value)
        return datetime(1900 + int(value[0]), 1 + int(value[1]), 1 + int(value[2]), int(value[3]), int(value[4]), int(value[5])) - datetime(1900, 1, 1)
    elif valueType is list:
        temp = json.loads(value)
        res = []
        for t in temp:
            res.append(parse(t[1], eval(t[0])))
        return res
    elif valueType is dict:
        temp = json.loads(value)
        res = {}
        for k, v in temp.items():
            res[parse(k, eval(v[0]))] = parse(v[2], eval(v[1]))
        return res
    raise Exception("Unsupported type to parse: %s" % valueType)
