import logging
import subprocess
from functools import wraps

try:
    import cec # python-cec
except:
    pass


def try_catch(defaultReturn=None, message=None):
    def wrapper(decorator):
        @wraps(decorator)
        def wrapped_decorator(*args, **kwargs):
            try:
                return decorator(*args, **kwargs)
            except:
                logging.exception(message or "Failed to execute function: " + decorator.__name__)
                return defaultReturn
        return wrapped_decorator

    return wrapper


@try_catch()
def call_system(cmd):
    proc = subprocess.Popen(
        cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, shell=True)
    return proc.communicate()


@try_catch(None)
def init_cec():
    cec.init()
    return cec
