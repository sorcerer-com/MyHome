﻿var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("object-editor-item", {
        template: template,
        props: ["value", "value-info"],
        data: function () {
            return {
                debounceOnDictKeyChange: debounce(this.onDictKeyChange, 1000), // set only after no changes in 1 sec
            }
        },
        computed: {
            localDateTime: {
                get: function () {
                    return this.value.substr(0, 16);
                },
                set: function (newValue) {
                    this.value = newValue + ":00";
                }
            }
        },
        methods: {
            getTitle: function () {
                return (this.value + "\n" + this.valueInfo.hint).trim();
            },
            getListValueInfo: function () {
                return { ...this.valueInfo.genericTypes[0], hint: this.valueInfo.hint, setting: this.valueInfo.setting };
            },
            onListChange: function (index, value) {
                this.value[index] = value;
                this.$emit("change", this.value);
            },
            getDictValueInfo: function (typeIdx) {
                return { ...this.valueInfo.genericTypes[typeIdx], hint: this.valueInfo.hint, setting: this.valueInfo.setting };
            },
            onDictKeyChange: function (oldKey, newKey) {
                if (oldKey != newKey) {
                    Vue.set(this.value, newKey, this.value[oldKey]);
                    Vue.delete(this.value, oldKey);
                    this.$emit("change", this.value);
                }
            },
            onDictValueChange: function (key, value) {
                Vue.set(this.value, key, value);
                this.$emit("change", this.value);
            },
            addDictItem: function () {
                Vue.set(this.value, "", "");
            },
            delDictItem: function (name) {
                Vue.delete(this.value, name);
            }
        },
        watch: {
            value: function () {
                this.$emit("change", this.value);
            }
        }
    });
});