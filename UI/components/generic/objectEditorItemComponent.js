var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("object-editor-item", {
        template: template,
        props: ["value", "value-type"],
        data: function () {
            return {
            }
        },
        methods: {
            getEnumValues: function (type) {
                if (!type.startsWith("Enum"))
                    return [];
                return type.replace("Enum (", "").replace(")", "").split(", ");
            },
            getListType: function (type) {
                return type.replace("List <", "").replace(">", "");
            }
        }
    });
});