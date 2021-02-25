$.get("./components/roomCardComponent.html", template => {
    Vue.component('room-card', {
        template: template,
        props: ['name'],
        data: function () {
            return {
            }
        }
    });
});