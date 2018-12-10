from Utils.Decorators import type_check

# http://www.valuedlessons.com/2008/04/events-in-python.html


class Event(object):
    """ Class used for creating events. """

    @type_check
    def __init__(self) -> None:
        """ Initialize an instance of the Event class. """

        self.handlers = set()

    @type_check
    def handle(self, handler: callable) -> object:
        """ Add new event handler.

        Arguments:
                handler {callable} -- The new event handler.

        Returns:
                Event -- self
        """

        self.handlers.add(handler)
        return self

    @type_check
    def unhandle(self, handler: callable) -> object:
        """ Remove event handler.

        Arguments:
                handler {callable} -- The event handler which will be removed.

        Raises:
                ValueError -- If the handler isn't in the list.

        Returns:
                Event -- self
        """

        try:
            self.handlers.remove(handler)
        except:
            raise ValueError(
                "Handler is not handling this event, so cannot unhandle it.")
        return self

    @type_check
    def fire(self, *args, **kargs) -> None:
        """ Trigger the event. """

        for handler in self.handlers:
            handler(*args, **kargs)

    @type_check
    def getHandlerCount(self) -> int:
        """ Gets the count of the event handlers.

        Returns:
                int -- Count of the event handlers.
        """

        return len(self.handlers)

    __iadd__ = handle
    __isub__ = unhandle
    __call__ = fire
    __len__ = getHandlerCount
