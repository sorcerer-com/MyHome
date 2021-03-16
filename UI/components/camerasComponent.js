var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("cameras", {
        template: template,
        props: ["cameras"],
        methods: {
            moveCamera: moveCamera
        },
        data: function () {
            return {
            }
        }
    });
});