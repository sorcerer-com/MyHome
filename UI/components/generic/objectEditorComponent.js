var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("object-editor", {
        template: template,
        props: ["object", "onsave"],
        data: function () {
            return {
                error: ""
            }
        },
        methods: {
            save: function () {
                this.onsave(this.object).fail(response => {
                    this.error = "Error: " + response.responseText;
                });
            }
        }
    });
});