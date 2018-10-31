import logging
from functools import wraps
from inspect import getfullargspec
from typing import get_type_hints


# https://aboutsimon.com/blog/2018/04/04/Python3-Type-Checking-And-Data-Validation-With-Type-Hints.html
def type_check(decorator: callable) -> callable:
    """ Type check decorator which check if the hit types matched.

    Arguments:
        decorator {callable} -- Function which will be decorated.

    Returns:
        callable -- Wrapped decorator.
    """

    def validate_input(decorator, **kwargs):
        hints = get_type_hints(decorator)

        # iterate all type hints
        for attr_name, attr_type in hints.items():
            if attr_name == "return":
                continue

            if (attr_name in kwargs) and (not isinstance(kwargs[attr_name], attr_type)):
                raise TypeError("Argument %r is not of type %s" %
                                (attr_name, attr_type))

    @wraps(decorator)
    def wrapped_decorator(*args, **kwargs):
        # translate *args into **kwargs
        func_args = getfullargspec(decorator)[0]
        kwargs.update(dict(zip(func_args, args)))

        # check for docstring
        if decorator.__doc__ is None:
            raise SyntaxWarning("Function doesn't have docstring: %s.%s" % (
                decorator.__module__, decorator.__name__))

        # check for hints count
        func_args_count = len(func_args) if "self" not in func_args else len(func_args) - 1
        hits_count = max(0, len(get_type_hints(decorator)) -
                         1)  # without 'return'
        if func_args_count != hits_count:
            raise SyntaxWarning(
                "Function doesn't have appropriate type hints: %s.%s" % (decorator.__module__, decorator.__name__))

        validate_input(decorator, **kwargs)
        return decorator(**kwargs)

    return wrapped_decorator


def try_catch(message: str = "Exception was thrown", defaultReturn=None) -> callable:
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
