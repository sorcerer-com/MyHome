import base64
import json
import logging
import socket
from datetime import datetime, timedelta

import paho.mqtt.client
import psutil

import utils


class Agent:

    def __init__(self, config):
        self._config = config
        self._hostname = socket.gethostname()
        self._next_update_2 = datetime.now()

        self._mqtt = paho.mqtt.client.Client(self._hostname + "Client")
        self._cec = None
        self._cec_devices = {}

    def setup(self):
        self._mqtt.on_connect = self._mqtt_on_connect
        self._mqtt.on_message = self._mqtt_on_message
        self._mqtt.username_pw_set(
            self._config["MQTT"]["username"], base64.b64decode(self._config["MQTT"]["password"]))
        self._mqtt.connect(
            self._config["MQTT"]["host"], int(self._config["MQTT"]["port"]))

        self._mqtt.loop_start()

        self._cec = utils.init_cec()

        features = {"telemetry": True, "CEC": self._cec is not None}
        logging.info(f"Supported features: {features}")
        self._mqtt.publish(
            f"tele/{self._hostname}/FEATURES", json.dumps(features), retain=True)

    def update(self):
        self._send_telemetry()

        if datetime.now() > self._next_update_2:
            self._next_update_2 = datetime.now(
            ) + timedelta(minutes=int(self._config["AGENT"]["update_2_interval"]))
            self._update_cec_devices()

    def stop(self):
        self._mqtt.loop_stop()
        self._mqtt.disconnect()

    def _mqtt_on_connect(self, client, userdata, flags, rc):
        logging.info(
            f"MQTT client {client._client_id} connected to {client._host}:{client._port}")

        self._mqtt.subscribe(f"cmnd/{self._hostname}/cec")
        
    @utils.try_catch()
    def _mqtt_on_message(self, client, userdata, msg):
        logging.info(
            f"MQTT message received with topic '{msg.topic}': {msg.payload}")

        payload = json.loads(msg.payload.decode())
        if msg.topic == f"cmnd/{self._hostname}/cec":
            self._handle_cec_cmd(payload)

    @utils.try_catch()
    def _send_telemetry(self):
        result = {}

        output = utils.call_system(
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

    @utils.try_catch()
    def _update_cec_devices(self):
        if self._cec is None:
            return

        self._cec.init()
        self._cec_devices = {
            f"{item[1].osd_string}_{item[0]}": item[1] for item in self._cec.list_devices().items()}

        devices = []
        for item in self._cec_devices.items():
            try:
                is_on = item[1].is_on()
            except:
                is_on = None
            devices.append({"address": item[0], "physical_address": item[1].physical_address, "cec_version": item[1].cec_version,
                            "language": item[1].language, "is_on": is_on})
        logging.info(f"CEC devices: {devices}")
        self._mqtt.publish(
            f"tele/{self._hostname}/CEC_DEVICES", json.dumps(devices))

    @utils.try_catch()
    def _handle_cec_cmd(self, payload):
        # expected payload: { "address": "...", "command": "power_on/standby/transmit", "args": [] }
        # address is not device.address, but {device.osd_string}_{device.address}
        # to change source to HDMI 2: {"command": "transmit", "args": [15, 130, "2000"]} - cec.transmit(cec.CECDEVICE_BROADCAST, cec.CEC_OPCODE_ACTIVE_SOURCE, b'\x20\x00')
        # https://www.cec-o-matic.com/
        logging.info(f"Processing CEC command: {payload}")
        if self._cec is None:
            logging.warn(f"CEC is not supported")
            return

        if "command" not in payload:
            logging.warn("Invalid CEC command")
            return

        # device if address is provided else cec
        target = self._cec_devices[payload["address"]] if "address" in payload and payload["address"] in self._cec_devices else self._cec
        func = getattr(target, payload["command"])
        if payload["command"] == "transmit":
            payload["args"][-1] = bytes.fromhex(payload["args"][-1])
        res = func(*payload["args"]) if "args" in payload else func()
        logging.info(f"Result: {res}")

        self._update_cec_devices()
