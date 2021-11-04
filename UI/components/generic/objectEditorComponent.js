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
                if (type.startsWith("List")) {
                    let listType = type.replace("List <", "").replace(">", "");
                    return this.isSupportedType(listType);
                } else if (type.startsWith("Dictionary")) {
                    let dictTypes = splitTypes(type.replace("Dictionary <", "").replace(">", ""));
                    return this.isSupportedType(dictTypes[0]) && this.isSupportedType(dictTypes[1]);
                } else if (type.startsWith("ValueTuple")) {
                    let tupleTypes = splitTypes(type.replace("ValueTuple <", "").replace(">", ""));
                    return tupleTypes.every(t => this.isSupportedType(t));
                }
                return type == "Boolean" || type.startsWith("Enum") || type.startsWith("Int") ||
                    type == "Double" || type == "DateTime" || type == "TimeSpan" || type == "String" ||
                    type.startsWith("Select");
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