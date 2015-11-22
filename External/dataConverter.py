import json

if __name__ == "__main__":
	with open("data.dat", "r", encoding="utf8") as file:
		data = json.load(file)

	data = data["Sensors"]
	print(data.keys())

	newData = {}
	for name, _id in data["ids"].items():
		print(name)
		print(data[str(_id)]["subNames"])
		print(len(data[str(_id)]))

		newData[name] = ["str", "dict", {}]
		newData[name][2]["data"] = ["str", "dict", {}]
		
		for key, value in data[str(_id)].items():
			if key == "subNames":
				continue
				
			newData[name][2]["data"][2][key] = ["datetime", "dict", {}]
			for i in range(0, len(data[str(_id)]["subNames"])):
				subName = data[str(_id)]["subNames"][i]
				_type = type(value[i]).__name__
				newData[name][2]["data"][2][key][2][subName] = ["str", _type, str(value[i])]

	with open("newData.json", "w", encoding="utf8") as file:
		file.write(json.dumps(newData, indent=4, ensure_ascii=True))
