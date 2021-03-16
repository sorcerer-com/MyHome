function getRooms() {
    return $.get("/api/rooms");
}

function setRoom(roomName, data) {
    return $.post(`/api/rooms/${roomName}`, data);
}

function getSystem(systemName) {
    return $.get(`/api/systems/${systemName}`);
}

function callSystem(systemName, funcName, ...args) {
    let data = Object.assign({}, args);
    return $.post(`/api/systems/${systemName}/${funcName}`, data);
}

function getSensorData(sensorName, valueType) {
    return $.get(`/api/sensors/${sensorName}/data/${valueType}`);
}

function getCameraImage(cameraName) {
    return $.get(`/api/cameras/${cameraName}/image`);
}

function moveCamera(cameraName, movementType) {
    return $.post(`/api/cameras/${cameraName}/move?movementType=${movementType}`)
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
                type: 'line',
                pointRadius: 0,
                borderWidth: 2
            }]
        },
        options: {
            scales: {
                xAxes: [{
                    type: 'time',
                    ticks: {
                        beginAtZero: true,
                        fontColor: 'white'
                    }
                }],
                yAxes: [{
                    ticks: {
                        beginAtZero: true,
                        fontColor: 'white'
                    }
                }]
            },
            tooltips: {
                intersect: false,
            }
        }
    };
    return new Chart(canvas, cfg);
}

function updateChartData(chart, data) {
    chart.data.datasets[0].data = data;
    chart.update();
}