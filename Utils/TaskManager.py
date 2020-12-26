import logging
from multiprocessing.pool import AsyncResult, ThreadPool

from Utils.Decorators import type_check
from Utils.Singleton import Singleton

logger = logging.getLogger(__name__.split(".")[-1])


class TaskManager(Singleton):
    """ Task manager class. """

    @type_check
    def __init__(self) -> None:
        """ Initialize an instance of the TaskManager class. """

        self._taskResults = {}
        self._threadPool = ThreadPool()

    @type_check
    def execute(self, service: object, func: callable, args: list = None, kwargs: dict = None, callback: callable = None, errorCallback: callable = None) -> AsyncResult:
        """ Execute function by the thread pool if there is no already running task for the service.

        Args:
            service ([object]): Service that this execution belongs to.
            func ([callable]): Function to be executed.
            args ([list], optional): List of arguments. Defaults to None.
            kwargs ([dict], optional): List of keyword arguments. Defaults to None.
            callback ([callable], optional): Success callback function. Defaults to None.
            errorCallback ([callable], optional): Error callback function. Defaults to None.

        Returns:
            AsyncResult: Result of the execution.
        """

        # if service isn't None or the result of previous execution is "ready"
        if service is not None and service in self._taskResults and not self._taskResults[service].ready():
            return None

        args = args or []
        kwargs = kwargs or {}

        result = self._threadPool.apply_async(func, args, kwargs, callback, errorCallback)
        self._taskResults[service] = result
        return result
