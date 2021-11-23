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
            updateLocalValue: function () {
                if (this.valueType == "DateTime")
                    this.localValue = this.value.substr(0, 16);
                else
                    this.localValue = this.value;
            },

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
            },
            getDictTypes: function () {
                return splitTypes(this.valueType.replace("Dictionary <", "").replace(">", ""));
            },
            delDictItem: function (name) {
                Vue.delete(this.localValue, name);
            },
            addDictItem: function () {
                Vue.set(this.localValue, "", "");
            },
            onDictKeyChange: function (oldKey, newKey) {
                if (oldKey != newKey) {
                    Vue.set(this.localValue, newKey, this.localValue[oldKey]);
                    Vue.delete(this.localValue, oldKey);
                    this.$emit("change", this.localValue);
                }
            },
            onDictValueChange: function (key, value) {
                Vue.set(this.localValue, key, value);
                this.$emit("change", this.localValue);
            },
            getSelectValues: function () {
                if (!this.valueType.startsWith("Select"))
                    return [];
                return this.valueType.replace("Select: ", "").split(", ");
            }
        },
        created: function () {
            this.updateLocalValue();
        },
        watch: {
            value: function () {
                this.updateLocalValue();
            },
            localValue: function () {
                this.$emit("change", this.localValue);
            }
        }
    });
});