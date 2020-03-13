import json
import logging
import logging.handlers
import re
from datetime import datetime, timedelta

from Utils.Decorators import try_catch, type_check
from Utils.LoggingFilter import LoggingFilter


@type_check
def setupLogging(fileName: str, logLevel: int = logging.INFO, showInConsole: bool = True, useBufferHandler: bool = True) -> None:
    """ Setup Logging module.

    Arguments:
            fileName {str} -- Path to the file which will contains the logs.

    Keyword Arguments:
            logLevel {int} -- Log level of the file and consol handlers. (default: {logging.INFO})
            showInConsole {bool} -- Indicate whether the logs will be shown in the standart output. (default: {True})
            useBufferHandler {bool} -- Indicate whether the logs will be collected in the buffer. (default: {True})
    """

    logger = logging.getLogger()
    # already setupped
    if len(logger.handlers) > 0 and isinstance(logger.handlers[0], logging.handlers.RotatingFileHandler):
        return

    logger.handlers.clear()
    logger.setLevel(logging.DEBUG)
    formatter = logging.Formatter(
        "%(asctime)-20s %(name)-15s %(levelname)-8s %(message)s", "%d/%m/%Y %H:%M:%S")

    # add RotatingFileHandler
    file = logging.handlers.RotatingFileHandler(
        fileName, maxBytes=1024*1024, backupCount=3, encoding="utf-8")
    file.setLevel(logLevel)
    file.setFormatter(formatter)
    logger.addHandler(file)

    noWerkzeugInfoFilter = LoggingFilter(lambda r: not (
        r.name == "werkzeug" and r.levelno == logging.INFO))
    if showInConsole:
        # add Console handler
        console = logging.StreamHandler()
        console.setLevel(logLevel)
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
    """ Return list of log records.

    Returns:
            list -- List of log records.
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
            if not isinstance(staticValue, property) or \
               staticValue.fget is None or \
               staticValue.fset is None:
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
        return json.dumps(serializable(value))
    elif valueType is dict:
        return json.dumps(serializable(value))
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
        return deserializable(json.loads(value))
    elif valueType is dict:
        return deserializable(json.loads(value))

    if value == "None":
        return None
    raise Exception(f"Unsupported type to parse: {valueType}")


@type_check
def serializable(value: object) -> object:
    """ Convert to serializable object.

    Arguments:
        value {object} -- Value to be converted.

    Returns:
        object -- Serializable object.
    """

    valueType = type(value)
    if valueType is list:
        return [(type(v).__name__, serializable(v)) for v in value]
    elif valueType is dict:
        return {string(k): (type(k).__name__, type(v).__name__, serializable(v)) for k, v in value.items()}

    return string(value)


@type_check
def deserializable(value: object, valueType: type = type(None)) -> object:
    """ Convert to deserializable object.

    Arguments:
        value {object} -- Value to be converted.

    Returns:
        object -- Deserializable object.
    """

    # pylint: disable=eval-used
    if valueType == type(None):
        valueType = type(value)

    if valueType is list:
        return [deserializable(v[1], eval(v[0])) for v in value]
    elif valueType is dict:
        return {parse(k, eval(v[0])): deserializable(v[2], eval(v[1])) for k, v in value.items()}

    return parse(value, valueType)
