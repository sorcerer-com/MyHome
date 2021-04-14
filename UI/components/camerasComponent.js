var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("cameras", {
        template: template,
        props: ["cameras"],
        data: function () {
            return {
            }
        },
        methods: {
            moveCamera: moveCamera,
            restartCamera: function (cameraName) {
                if (!confirm("Are you sure you want to restart camera?"))
                    return;

                restartCamera(cameraName);
            }
        }
    });
});