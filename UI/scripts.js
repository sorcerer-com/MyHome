function getRooms(settings = false) {
    return $.get(`./api/rooms?settings=${settings}`);
}

function createRoom() {
    return $.post("./api/rooms/create");
}

function setRoom(roomName, data) {
    return $.post(`./api/rooms/${roomName}`, data);
}

function deleteRoom(roomName) {
    return $.post(`./api/rooms/${roomName}/delete`);
}

function createDevice(roomName, deviceType) {
    return $.post(`./api/rooms/${roomName}/devices/create/${deviceType}`);
}

function setDevice(roomName, deviceName, data) {
    return $.post(`./api/rooms/${roomName}/devices/${deviceName}`, data);
}

function deleteDevice(roomName, deviceName) {
    return $.post(`./api/rooms/${roomName}/devices/${deviceName}/delete`);
}

function getSensorData(roomName, sensorName, valueType) {
    return $.get(`./api/rooms/${roomName}/sensors/${sensorName}/data/${valueType}`);
}

function moveCamera(roomName, cameraName, movementType) {
    return $.post(`./api/rooms/${roomName}/cameras/${cameraName}/move?movementType=${movementType}`)
}

function restartCamera(roomName, cameraName) {
    return $.post(`./api/rooms/${roomName}/cameras/${cameraName}/restart`)
}


function getSystems(settings = false) {
    return $.get(`./api/systems?settings=${settings}`);
}

function getSystem(systemName, settings = false) {
    return $.get(`./api/systems/${systemName}?settings=${settings}`);
}

function setSystem(systemName, data) {
    return $.post(`./api/systems/${systemName}`, data);
}

function callSystem(systemName, funcName, ...args) {
    let data = Object.assign({}, args);
    return $.post(`./api/systems/${systemName}/${funcName}`, data);
}

function createAction(actionType) {
    return $.post(`./api/systems/Actions/create/${actionType}`);
}

function createActionExecutor(executorType) {
    return $.post(`./api/systems/Actions/Executor/create/${executorType}`);
}

function setAction(actionName, data) {
    return $.post(`./api/systems/Actions/${actionName}`, data);
}

function deleteAction(actionName) {
    return $.post(`./api/systems/Actions/${actionName}/delete`);
}


function getConfig() {
    return $.get("./api/config");
}

function setConfig(data) {
    return $.post("./api/config", data);
}

function getLogs() {
    return $.get("./api/logs");
}

function getUpgradeAvailable() {
    return $.get("./api/upgrade");
}

function upgrade() {
    return $.post("./api/upgrade");
}

function restart() {
    return $.post("./api/restart");
}

function getSubTypes(typeName) {
    return $.get(`./api/types/${typeName}`);
}


function setRoomSecuritySystemEnabled(roomName, isEnabled) {
    setRoom(roomName, { IsSecuritySystemEnabled: isEnabled })
        .done(() => getRooms().done(rooms => Vue.set(this.$parent, "rooms", rooms)));
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
                mode: "index"
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

function splitTypes(list) {
    let split = list.split(", ");
    for (let i = 0; i < split.length; i++) {
        if (split[i].includes("<") && !split[i].includes(">")) {
            split[i] += ", " + split[i + 1];
            split.splice(i + 1);
            i--;
        }
    }
    return split;
}

// execute func once after no call for delay period of time
const debounce = function (func, delay) {
    let timer;
    return function () {     //anonymous function
        const context = this;
        const args = arguments;
        clearTimeout(timer);
        timer = setTimeout(() => {
            func.apply(context, args)
        }, delay);
    }
}