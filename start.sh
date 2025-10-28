#!/bin/bash
docker run -d -p 5000:5000 -v ~/MyHome:/MyHome:rw -w /MyHome --restart always --name MyHome \
	--health-cmd "curl --fail http://localhost:5000/api/status" --health-interval 30s --health-retries 3 \
	mcr.microsoft.com/dotnet/sdk:9.0 sh -c "apt update && apt install -y python3 python3-opencv && /usr/bin/dotnet run -c Release -launch-profile \"MyHome\""