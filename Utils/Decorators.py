import logging
from functools import wraps
from inspect import getfullargspec
from typing import get_type_hints


def type_check(decorator:callable) -> callable:
    """ Type check decorator which check if the hit types matched.
    
    Arguments:
        decorator {callable} -- Function which will be decorated.
    
    Returns:
        callable -- Wrapped decorator.
    """

    def validate_input(obj, **kwargs):
        hints = get_type_hints(obj)

        # iterate all type hints
        for attr_name, attr_type in hints.items():
            if attr_name == 'return':
                continue

            if (attr_name in kwargs) and (not isinstance(kwargs[attr_name], attr_type)):
                raise TypeError('Argument %r is not of type %s' % (attr_name, attr_type))

    @wraps(decorator)
    def wrapped_decorator(*args, **kwargs):
        # translate *args into **kwargs
        func_args = getfullargspec(decorator)[0]
        kwargs.update(dict(zip(func_args, args)))

        validate_input(decorator, **kwargs)
        return decorator(**kwargs)

    return wrapped_decorator

def try_catch(message:str = "Exception was thrown", defaultReturn=None) -> callable:
    """ Try-catch decorator which catch all types of Exceptions caught in the decorated function and log a message.
    
    Keyword Arguments:
        message {str} -- Message which will be logged when the Exception occurs. (default: {"Exception was thrown"})
        defaultReturn {any} -- The value that will be returned if the Exception occurs. (default: {None})
    
    Returns:
        callable -- Wrapped decorator.
    """

    def wrapper(decorator):
        @wraps(decorator)
        def wrapped_decorator(*args, **kwargs):
            try:
                return decorator(*args, **kwargs)
            except Exception:
                logger = logging.getLogger(decorator.__module__)
                logger.exception(message)
                return defaultReturn
        return wrapped_decorator
    
    return wrapper
