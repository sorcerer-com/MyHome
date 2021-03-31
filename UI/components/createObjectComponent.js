var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("create-object", {
        template: template,
        props: ["object", "onsave"],
        data: function () {
            return {
                error: ""
            }
        },
        methods: {
            getEnumValues: function (type) {
                if (!type.startsWith("Enum"))
                    return [];
                return type.replace("Enum (", "").replace(")", "").split(", ");
            },

            save: function () {
                this.onsave(this.object).fail(response => {
                    this.error = "Error: " + response.responseText;
                });
            }
        }
    });
});