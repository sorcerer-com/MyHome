import logging
import time
from configparser import ConfigParser
from logging.handlers import RotatingFileHandler

from agent import Agent

CONFIG_FILE = "agent.cfg"


logging_stdout_handler = logging.StreamHandler()
logging_file_handler = RotatingFileHandler(
    "agent.log", maxBytes=1024*1024, backupCount=2)
logging.root.handlers.clear()
logging.basicConfig(format="%(asctime)s.%(msecs)03d %(levelname)-7s %(message)s",
                    datefmt="%Y-%m-%d %H:%M:%S", level=logging.INFO,
                    handlers=[logging_stdout_handler, logging_file_handler])


def read_conf(file_path):
    logging.info("Read config file")
    config = {}
    parser = ConfigParser()
    parser.read(file_path)
    for section in parser.sections():
        if section not in config:
            config[section] = {}
        for key in parser[section]:
            config[section][key] = parser[section][key]
    logging.info(f"Configurations: {config}")
    return config


if __name__ == "__main__":
    try:
        logging.info("My Home Agent Started")
        config = read_conf(CONFIG_FILE)
        agent = Agent(config)
        agent.setup()
        while True:
            start = time.time()
            agent.update()
            elapsed = time.time() - start

            if elapsed > 0.1:
                logging.info(f"Update: {elapsed} sec")
            time.sleep(max(0.0, float(config["AGENT"]["sleep_time"]) - elapsed))

    except Exception as e:
        logging.exception("Agent exception")
    finally:
        agent.stop()
        logging.info("done\n")
