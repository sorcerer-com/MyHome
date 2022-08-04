
function createWebSocket() {
    let handlers = window.ws?.refreshHandlers;
    let protocol = window.location.protocol.includes("https") ? "wss" : "ws";
    let path = window.location.host + window.location.pathname;
    window.ws = new WebSocket(`${protocol}://${path}ws/refresh`);
    window.ws.lastMessage = new Date();
    window.ws.refreshHandlers = handlers || [];


    window.ws.addRefreshHandlers = function (handler) {
        window.ws.refreshHandlers.push(handler);
    }

    window.ws.removeRefreshHandlers = function (handler) {
        let idx = window.ws.refreshHandlers.indexOf(handler);
        if (idx != -1)
            window.ws.refreshHandlers.splice(idx, 1);
    }

    // call all refresh handlers
    for (let handler of window.ws.refreshHandlers)
        handler();


    window.ws.onopen = function (event) {
        console.debug(`${new Date().toISOString()} WebSocket opened: `);
        console.debug(event);
    }

    window.ws.onclose = function (event) {
        console.debug(`${new Date().toISOString()} WebSocket closed: `);
        console.debug(event);
    }

    window.ws.onerror = function (event) {
        console.warn(`${new Date().toISOString()} WebSocket error occurs:`);
        console.warn(event);
    }

    window.ws.onmessage = function (event) {
        //console.debug(`${new Date().toISOString()} WebSocket message received:`);
        //console.debug(event);
        window.ws.lastMessage = new Date();

        if (event.data == "refresh") {
            console.debug(`${new Date().toISOString()} WebSocket 'refresh' received`)
            // call all refresh handlers
            for (let handler of window.ws.refreshHandlers)
                handler();
        }
    }
}


if (window.WebSocket) {
    let retry = 0;
    // ping every 3 seconds
    setInterval(() => {
        if (retry > 10)
            return;

        if (window.ws.readyState == WebSocket.CLOSED) {
            retry += 1;
            window.ws.close();
            createWebSocket();
        } else if (window.ws.readyState == WebSocket.OPEN) {
            retry = 0;
            window.ws.send("ping");
            if (new Date() - window.ws.lastMessage > 10000) {
                console.warn("WebSocket no messages for more than 10 seconds");
                window.ws.close();
            }
        }
    }, 1000);

    createWebSocket();
}