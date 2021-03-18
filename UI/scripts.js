function getRooms() {
    return $.get("./api/rooms");
}

function setRoom(roomName, data) {
    return $.post(`./api/rooms/${roomName}`, data);
}

function getSystem(systemName) {
    return $.get(`./api/systems/${systemName}`);
}

function callSystem(systemName, funcName, ...args) {
    let data = Object.assign({}, args);
    return $.post(`./api/systems/${systemName}/${funcName}`, data);
}

function getSensorData(sensorName, valueType) {
    return $.get(`./api/sensors/${sensorName}/data/${valueType}`);
}

function getCameraImage(cameraName) {
    return $.get(`./api/cameras/${cameraName}/image`);
}

function moveCamera(cameraName, movementType) {
    return $.post(`./api/cameras/${cameraName}/move?movementType=${movementType}`)
}

function getLogs() {
    return $.get("./api/logs");
} 


function setRoomSecuritySystemEnabled(roomName, isEnabled) {
    setRoom(roomName, { IsSecuritySystemEnabled: isEnabled })
        .done(() => getRooms().done(rooms => updateVue({ rooms: rooms })));
}


function updateVue(data) {
    for (let key in data) {
        Vue.set(window.vue, key, data[key]);
    }
}

function showLineChart(canvas, data, label) {
    let cfg = {
        data: {
            datasets: [{
                label: label,
                backgroundColor: Chart.helpers.color("blue").alpha(0.1).rgbString(),
                borderColor: Chart.helpers.color("blue").alpha(0.5).rgbString(),
                data: data,
                type: "line",
                lineTension: 0,
                pointRadius: 0,
                borderWidth: 2
            }]
        },
        options: {
            scales: {
                xAxes: [{
                    type: "time",
                    ticks: {
                        fontColor: "white"
                    },
                    time: {
                        displayFormats: {
                            hour: "HH"
                        },
                        tooltipFormat: "DD MMM YYYY, HH:mm:ss"
                    }
                }],
                yAxes: [{
                    ticks: {
                        fontColor: "white"
                    }
                }]
            },
            tooltips: {
                intersect: false,
                mode: "x"
            },
            legend: {
                labels: {
                    fontColor: "white"
                }
            }
        }
    };
    return new Chart(canvas, cfg);
}

function updateChartData(chart, data) {
    chart.data.datasets[0].data = data;
    chart.update();
}
