import json

if __name__ == "__main__":
	with open("data-old.json", "r", encoding="utf8") as file:
		data = json.load(file)

	data = data["Sensors"]
	print(data.keys())
	
	with open("data-new.json", "r", encoding="utf8") as file:
		newData = json.load(file)

	startId = 6
	devices = newData["Systems"]["DevicesSystem"]["Devices"]["$values"]
	for name, sensor in data.items():
		# create Devices
		device = {}
		device["$id"] = startId
		startId += 1
		device["$type"] = "MyHome.Systems.Devices.MySensor, MyHome"
		device["Address"] = ""
		device["Token"] = sensor[2]["token"][2]

		device["Data"] = {}
		device["Data"]["$type"] = "System.Collections.Generic.Dictionary`2[[System.DateTime, System.Private.CoreLib],[MyHome.Systems.Devices.BaseSensor+SensorValue, MyHome]], System.Private.CoreLib";
		for time, item in sensor[2]["data"][2].items():
			tTime = time.replace(" ", "T")
			device["Data"][tTime] = {}
			device["Data"][tTime]["$id"] = startId
			startId += 1
			device["Data"][tTime]["$type"] = "MyHome.Systems.Devices.BaseSensor+SensorValue, MyHome"
			for subType, value in item[2].items():
				if value[2]  in ("False", "True"):
					device["Data"][tTime][subType] = 1.0 if value[2] == "True" else 0.0
				else:
					device["Data"][tTime][subType] = round(float(value[2]), 2)

		device["Owner"] = {"$ref": "3"}
		device["Name"] = name
		device["Room"] = {"$ref": "2"}

		devices.append(device)

	with open("data.json", "w", encoding="utf8") as file:
		file.write(json.dumps(newData, indent=2, ensure_ascii=True))