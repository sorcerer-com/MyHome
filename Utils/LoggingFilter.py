import logging
from Utils.Decorators import type_check


class LoggingFilter(logging.Filter):
    """ Filter instances are used to perform arbitrary filtering of LogRecords. """

    @type_check
    def __init__(self, pred: callable) -> None:
        """ Initialize a filter.

        Arguments:
            pred {callable} -- Predicate which will be used to check whether passed LogRecord should be filtered.
        """

        super().__init__()

        self.pred = pred

    @type_check
    def filter(self, record: logging.LogRecord) -> bool:
        """ Determine if the specified record is to be logged.

        Arguments:
            record {logging.LogRecord} -- Record which will be checked for filtering.

        Returns:
            bool -- True if the record should be filtered, otherwise false.
        """

        return self.pred(record)
