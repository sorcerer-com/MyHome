import logging
from functools import wraps
from inspect import getfullargspec
from typing import get_type_hints, Union


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
            if attr_name == "return" or attr_name not in kwargs or attr_type == type(None):
                continue

            if attr_type is callable and callable(kwargs[attr_name]):
                continue

            if hasattr(attr_type, "__args__"):
                attr_type = attr_type.__args__

            if kwargs[attr_name] is not None and not isinstance(kwargs[attr_name], attr_type):
                raise TypeError(
                    f"Argument {attr_name} is not of type {attr_type}")

    @wraps(decorator)
    def wrapped_decorator(*args, **kwargs):
        # translate *args into **kwargs
        func_args = getfullargspec(decorator)[0]
        kwargs.update(dict(zip(func_args, args)))

        # check for docstring
        if decorator.__doc__ is None:
            raise SyntaxWarning(
                f"Function doesn't have docstring: {decorator.__module__}.{decorator.__name__}")

        # check for hints count
        hints = get_type_hints(decorator)
        func_args_count = len(
            func_args) if "self" not in func_args else len(func_args) - 1
        if func_args_count != len(hints) - 1:  # without the 'return' hint
            raise SyntaxWarning(
                f"Function doesn't have appropriate type hints: {decorator.__module__}.{decorator.__name__}")

        validate_input(decorator, **kwargs)
        return decorator(**kwargs)

    return wrapped_decorator


def try_catch(message: str = "Exception was thrown", defaultReturn: object = None) -> callable:
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
