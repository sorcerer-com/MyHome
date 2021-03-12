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
    var data = Object.assign({}, args);
    return $.post(`/api/systems/${systemName}/${funcName}`, data);
}


function updateVue(data) {
    for (let key in data) {
        Vue.set(window.vue, key, data[key]);
    }
}
