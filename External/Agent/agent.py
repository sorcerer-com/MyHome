import base64
import json
import logging
import os
import socket
import time
from datetime import datetime, timedelta

import paho.mqtt.client
import psutil
import wakeonlan

import utils


class Agent:
    SupportedMediaFormats = [".mkv", ".avi", ".mov", ".wmv", ".mp4",
                             ".mpg", ".mpeg", ".m4v", ".3gp", ".mp3"]

    def __init__(self, config):
        self._config = config
        self._hostname = socket.gethostname()
        self._next_update = datetime.now()
        self._next_update_2 = datetime.now()
        self._media_refresh_time = datetime.now()

        self._mqtt = paho.mqtt.client.Client(self._hostname + "Client")
        self._media_player = None
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

        self._media_player = utils.init_media_player()
        self._cec = utils.init_cec()

        features = {"telemetry": True, "CEC": self._cec is not None,
                    "media": self._media_player is not None}
        logging.info(f"Supported features: {features}")
        self._mqtt.publish(
            f"tele/{self._hostname}/FEATURES", json.dumps(features), retain=True)

    def update(self):
        self._update_media()

        if datetime.now() > self._next_update:
            self._next_update = datetime.now(
            ) + timedelta(minutes=int(self._config["AGENT"]["update_interval"]))

            self._send_telemetry()

            if datetime.now() > self._media_refresh_time:
                self._media_refresh_time = datetime.now() + timedelta(days=1)
                self._update_media_list()

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
        self._mqtt.subscribe(f"cmnd/{self._hostname}/media")

    @utils.try_catch()
    def _mqtt_on_message(self, client, userdata, msg):
        logging.info(
            f"MQTT message received with topic '{msg.topic}': {msg.payload}")

        payload = json.loads(msg.payload.decode())
        if msg.topic == f"cmnd/{self._hostname}/cec":
            self._handle_cec_cmd(payload)
        if msg.topic == f"cmnd/{self._hostname}/media":
            self._handle_media_cmd(payload)

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
            f"tele/{self._hostname}/CEC_DEVICES", json.dumps(devices), retain=True)

    @utils.try_catch()
    def _update_media_list(self):
        if "MEDIA" not in self._config or not self._media_player:
            return

        media_list = {}
        for (key, mediaPath) in self._config["MEDIA"].items():
            if not key.startswith("path") or not os.path.isdir(mediaPath):
                continue
            media_list[mediaPath] = []
            for root, _, files in os.walk(mediaPath):
                for f in files:
                    if os.path.splitext(f)[1] not in Agent.SupportedMediaFormats:
                        continue
                    path = os.path.join(root, f)
                    media_list[mediaPath].append(
                        (os.path.relpath(path, mediaPath), os.path.getmtime(path)))

        logging.info(f"Media list: {media_list}")
        self._mqtt.publish(
            f"tele/{self._hostname}/MEDIA_LIST", json.dumps(media_list), retain=True)

    @utils.try_catch()
    def _update_media(self):
        if not self._media_player or str(self._media_player.get_state()) in ("State.NothingSpecial", "State.Stopped"):
            return

        if str(self._media_player.get_state()) == "State.Ended":
            self._media_player.stop()

        path = self._media_player.get_media().get_mrl() if str(
            self._media_player.get_state()) != "State.Stopped" else ""
        media_info = {"playing": path, "state": str(self._media_player.get_state()), "volume": self._media_player.audio_get_volume(),
                      "time": self._media_player.get_time(), "length": self._media_player.get_length()}
        if datetime.now() > self._next_update:
            logging.info(f"Media info: {media_info}")
        self._mqtt.publish(
            f"tele/{self._hostname}/MEDIA", json.dumps(media_info))

    @utils.try_catch()
    def _handle_cec_cmd(self, payload):
        # expected payload: { "address": "...", "command": "power_on/standby/transmit", "args": [] }
        # address is not device.address, but {device.osd_string}_{device.address}
        # to change source to HDMI 2: {"command": "transmit", "args": [15, 130, "2000"]} - cec.transmit(cec.CECDEVICE_BROADCAST, cec.CEC_OPCODE_ACTIVE_SOURCE, b'\x20\x00')
        # https://www.cec-o-matic.com/
        logging.info(f"Processing CEC command: {payload}")
        if self._cec is None:
            logging.warn("CEC is not supported")
            return

        if "command" not in payload:
            logging.warn("Invalid CEC command")
            return

        # device if address is provided else cec
        target = self._cec_devices[payload["address"]
                                   ] if "address" in payload and payload["address"] in self._cec_devices else self._cec
        func = getattr(target, payload["command"])
        if payload["command"] == "transmit":
            payload["args"][-1] = bytes.fromhex(payload["args"][-1])
        res = func(*payload["args"]) if "args" in payload else func()
        logging.info(f"Result: {res}")

        self._update_cec_devices()

    @utils.try_catch()
    def _handle_media_cmd(self, payload):
        logging.info(f"Processing media command: {payload}")
        if self._media_player is None:
            logging.warn("Media is not supported")
            return

        for key in payload:
            if key == "refresh" and payload["refresh"]:
                self._update_media_list()
            elif key == "play":
                # if TV MAC address is defined try to wake it up
                if "tv_mac" in self._config["MEDIA"]:
                    wakeonlan.send_magic_packet(self._config["MEDIA"]["tv_mac"])
                # if cec supported - power on tv and switch to TV
                if self._cec is not None:
                    self._cec.set_active_source()
                    self._cec.transmit(self._cec.CECDEVICE_BROADCAST, self._cec.CEC_OPCODE_ACTIVE_SOURCE, bytes.fromhex(self._config["MEDIA"]["cec_source"]))
                    
                self._media_player.set_fullscreen(False)
                self._media_player.set_mrl(
                    payload["play"], ":subsdec-encoding=Windows-1251")  # set default encoding to Cyrillic
                self._media_player.play()
                # set fullscreen after media start
                while self._media_player.get_time() == 0:
                    time.sleep(0.1)
                self._media_player.set_fullscreen(True)
            elif key == "stop" and payload["stop"]:
                self._media_player.set_pause(False)
                self._media_player.set_time(
                    self._media_player.get_length() - 100)  # stop by forward to the end (to send update)
                # self._media_player.stop()
            elif key == "pause":
                self._media_player.set_pause(bool(payload["pause"]))
            elif key == "volume":
                self._media_player.audio_set_volume(
                    max(int(payload["volume"]), 0))
            elif key == "time":
                self._media_player.set_time(int(payload["time"]))

        self._update_media()
