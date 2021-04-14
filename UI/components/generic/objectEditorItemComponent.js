var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("object-editor-item", {
        template: template,
        props: ["value", "value-type"],
        data: function () {
            return {
                localValue: this.value
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
            },
            onListChange: function (index, value) {
                this.localValue[index] = value;
                this.$emit("change", this.localValue);
            }
        },
        watch: {
            localValue: function () {
                this.$emit("change", this.localValue);
            }
        }
    });
});