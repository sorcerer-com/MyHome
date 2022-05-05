var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("object-editor", {
        template: template,
        props: ["object", "settings", "onsave"],
        data: function () {
            return {
                showEditor: false,
                error: ""
            }
        },
        computed: {
            json: {
                get: function () {
                    console.log(this.getJsonSchema(this.object["$subtypes"]));

                    let filteredObject = Object.keys(this.object)
                        .filter(key => key[0] != "$" && (this.settings == null || this.object['$subtypes'][key].setting == this.settings))
                        .reduce((cur, key) => { return Object.assign(cur, { [key]: this.object[key] }) }, {});
                    return JSON.stringify(filteredObject, null, 2);
                },
                set: function (newValue) {
                    let obj = JSON.parse(newValue);
                    Object.keys(obj).forEach(key => this.object[key] = obj[key]);
                }
            }
        },
        methods: {
            onItemChange: function (name, value) {
                this.object[name] = value;
            },
            save: function () {
                this.onsave(this.object).fail(response => {
                    this.error = "Error: " + response.responseText;
                });
            },

            getJsonSchema: function (subtype) {
                let schema = {};
                if (!subtype.type) {
                    schema.type = "object";
                    schema.properties = {};
                    for (let key of Object.keys(subtype)) {
                        schema.properties[key] = this.getJsonSchema(subtype[key]);
                    }
                } else {
                    schema.type = subtype.type.toLowerCase();
                    schema.description = subtype.hint;

                    if (subtype.type.startsWith("Int") || subtype.type == "Double")
                        schema.type = "number";
                    else if (subtype.type == "DateTime") {
                        schema.type = "string";
                        schema.pattern = "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}$";
                    }
                    else if (subtype.type == "TimeSpan") {
                        schema.type = "string";
                        schema.pattern = "^([0-9]*\\.)?[0-9]{2}:[0-9]{2}:[0-9]{2}$";
                    }
                    else if (subtype.type == "ValueTuple") {
                        schema.type = "string";
                        schema.pattern = "^\\(.*, .*\\)$";
                    }
                    else if (subtype.enums) {
                        delete schema.type;
                        schema.enum = subtype.enums.map(v => subtype.type + "." + v);
                    }
                    else if (subtype.type == "select") {
                        delete schema.type;
                        schema.enum = Object.keys(subtype.select).concat([""]);
                    }
                    else if (subtype.type == "List") {
                        schema.type = "array";
                        schema.items = this.getJsonSchema(subtype["genericTypes"][0]);
                    }
                    else if (subtype.type == "Dictionary") {
                        schema.type = "object";
                        schema.additionalProperties = this.getJsonSchema(subtype["genericTypes"][1]);
                    }
                }
                return schema;
            }
        },
        watch: {
            "object": function () {
                this.showEditor = false;
            }
        }
    });
});