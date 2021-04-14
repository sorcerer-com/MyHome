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
            isSupportedType: function (type) {
                if (type.startsWith('List')) {
                    let listType = type.replace("List <", "").replace(">", "");
                    return this.isSupportedType(listType);
                }
                return type == 'Boolean' || type.startsWith('Enum') || type.startsWith('Int') ||
                    type == 'Double' || type == 'DateTime' || type == 'String';
            },
            onItemChange: function (name, value) {
                this.object[name] = value;
            },
            save: function () {
                this.onsave(this.object).fail(response => {
                    this.error = "Error: " + response.responseText;
                });
            }
        }
    });
});