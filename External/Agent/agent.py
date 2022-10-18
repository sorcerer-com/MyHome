import logging
from shutil import disk_usage
import subprocess

import paho.mqtt.client
import psutil
import socket
import json


class Agent:

    def __init__(self, config):
        self._config = config
        self._hostname = socket.gethostname()

        self._mqtt = paho.mqtt.client.Client(self._hostname + "Client")

    def setup(self):
        self._mqtt.on_connect = self._mqtt_on_connect
        self._mqtt.on_message = self._mqtt_on_message
        self._mqtt.username_pw_set(
            self._config["MQTT"]["username"], self._config["MQTT"]["password"])
        self._mqtt.connect(
            self._config["MQTT"]["host"], int(self._config["MQTT"]["port"]))

        self._mqtt.loop_start()

    def update(self):
        self._sendTelemetry()

    def stop(self):
        self._mqtt.loop_stop()
        self._mqtt.disconnect()

    def _mqtt_on_connect(self, client, userdata, flags, rc):
        logging.info(
            f"MQTT client {client._client_id} connected to {client._host}:{client._port}")

    def _mqtt_on_message(self, client, userdata, msg):
        logging.info(
            f"MQTT message received with topic '{msg.topic}': {msg.payload}")

    def _sendTelemetry(self):
        result = {}

        output = self._callSystem(
            ["cat /sys/class/thermal/thermal_zone0/temp"])
        if output and output[0]:
            result["cpu_temp"] = int(output[0]) / 1000

        result["cpu_usage"] = psutil.cpu_percent()
        result["mem_usage"] = psutil.virtual_memory().percent
        result["disk_usage"] = {}
        for disk in psutil.disk_partitions():
            result["disk_usage"][disk.mountpoint] = psutil.disk_usage(
                disk.mountpoint).percent
        result["net_sent"] = psutil.net_io_counters().bytes_sent
        result["net_recv"] = psutil.net_io_counters().bytes_recv

        logging.info(f"Telemetry: {result}")
        self._mqtt.publish(f"tele/{self._hostname}/SENSOR", json.dumps(result))

    def _callSystem(self, cmd):
        try:
            proc = subprocess.Popen(
                cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, shell=True)
            return proc.communicate()
        except Exception as e:
            logging.debug(e)
