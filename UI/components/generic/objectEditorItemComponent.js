var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("object-editor-item", {
        template: template,
        props: ["value", "value-type", "hint"],
        data: function () {
            return {
                localValue: null
            }
        },
        methods: {
            getTitle: function () {
                return (this.localValue + "\n" + this.hint).trim();
            },
            getEnumValues: function () {
                if (!this.valueType.startsWith("Enum"))
                    return [];
                return this.valueType.replace("Enum (", "").replace(")", "").split(", ");
            },
            getListType: function () {
                return this.valueType.replace("List <", "").replace(">", "");
            },
            onListChange: function (index, value) {
                this.localValue[index] = value;
                this.$emit("change", this.localValue);
            }
        },
        created: function () {
            if (this.valueType == "DateTime")
                this.localValue = this.value.substr(0, 16);
            else
                this.localValue = this.value;
        },
        watch: {
            localValue: function () {
                this.$emit("change", this.localValue);
            }
        }
    });
});