$(_ => {
    $.get("/api/rooms", rooms =>
        window.vue = new Vue({
            el: '#vue-content',
            data: {
                roomNames: rooms
            }
        })
    );
});
