$.postJsonBody = function(url, data) {
    return $.ajax({
        type: 'POST',
        data: typeof data == "string" ? data : JSON.stringify(data),
        contentType: 'application/json',
        url: url
    });
}

$.postFormData = function (url, formData) {
    return $.ajax({
        url: url,
        type: 'POST',
        processData: false, // important
        contentType: false, // important
        data: formData
    });
}


function getRooms() {
    return $.get(`./api/rooms`);
}

function createRoom() {
    return $.post("./api/rooms/create");
}

function setRoom(roomName, data) {
    return $.postJsonBody(`./api/rooms/${roomName}`, data);
}

function deleteRoom(roomName) {
    return $.post(`./api/rooms/${roomName}/delete`);
}

function createDevice(roomName, deviceType) {
    return $.post(`./api/rooms/${roomName}/devices/create/${deviceType}`);
}

function setDevice(roomName, deviceName, data) {
    return $.postJsonBody(`./api/rooms/${roomName}/devices/${deviceName}`, data);
}

function callDevice(roomName, deviceName, funcName, ...args) {
    let data = Object.assign({}, args);
    return $.post(`./api/rooms/${roomName}/devices/${deviceName}/${funcName}`, data);
}

function deleteDevice(roomName, deviceName) {
    return $.post(`./api/rooms/${roomName}/devices/${deviceName}/delete`);
}

function getSensorData(roomName, sensorName, valueType) {
    return $.get(`./api/rooms/${roomName}/sensors/${sensorName}/data/${valueType}`);
}

function setSensorData(roomName, sensorName, valueType, data) {
    return $.postJsonBody(`./api/rooms/${roomName}/sensors/${sensorName}/data/${valueType}`, data);
}

function moveCamera(roomName, cameraName, movementType) {
    return $.post(`./api/rooms/${roomName}/cameras/${cameraName}/move?movementType=${movementType}`)
}

function restartCamera(roomName, cameraName) {
    return $.post(`./api/rooms/${roomName}/cameras/${cameraName}/restart`)
}


function getSystems() {
    return $.get(`./api/systems`);
}

function getSystem(systemName) {
    return $.get(`./api/systems/${systemName}`);
}

function setSystem(systemName, data) {
    return $.postJsonBody(`./api/systems/${systemName}`, data);
}

function callSystem(systemName, funcName, ...args) {
    let data = Object.assign({}, args);
    return $.post(`./api/systems/${systemName}/${funcName}`, data);
}

function createAction(actionType) {
    return $.post(`./api/systems/Actions/create/${actionType}`);
}

function setAction(actionName, data) {
    return $.postJsonBody(`./api/systems/Actions/${actionName}`, data);
}

function deleteAction(actionName) {
    return $.post(`./api/systems/Actions/${actionName}/delete`);
}

function triggerAction(actionName) {
    return $.post(`./api/systems/Actions/${actionName}/trigger`);
}


function getConfig() {
    return $.get("./api/config");
}

function setConfig(data) {
    return $.postJsonBody("./api/config", data);
}

function getBackupMode() {
    return $.get("./api/backup-mode");
}

function setBackupMode(data) {
    return $.postJsonBody("./api/backup-mode", data);
}

function getLogs() {
    return $.get("./api/logs");
}

function setMap(data) {
    return $.postFormData("./api/map", data);
}

function sendRecord(data) {
    return $.postFormData("./api/systems/Assistant/processRecord", data);
}

function getNotifications() {
    return $.get("./api/notifications");
}

function removeNotification(message) {
    return $.post(`./api/notifications/${message}/delete`);
}

function snoozeAlert(message, interval) {
    return $.post(`./api/notifications/${message}/snooze?interval=${interval}`);
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

function getTypescriptModels() {
    return $.get(`./api/typescript-models`);
}