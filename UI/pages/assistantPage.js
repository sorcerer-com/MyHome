setComponent("assistant-page", {
    data: function () {
        return {
            assistant: {},
            argumentMapping: null,
            requestMapping: null,

            edit: {
                name: null,
                newName: null,
                operation: null,
                args: null
            },
            message: null
        }
    },
    methods: {
        refreshData: function () {
            getSystem("Assistant").done(assistant => {
                this.assistant = assistant;
                // add only once
                if (!this.argumentMapping) {
                    this.argumentMapping = [];
                    Object.keys(this.assistant.ArgumentMapping).forEach(argument =>
                        this.argumentMapping.push({
                            "argument": argument,
                            "value": this.assistant.ArgumentMapping[argument]
                        })
                    );
                }
                if (!this.requestMapping) {
                    this.requestMapping = [];
                    Object.keys(this.assistant.RequestMapping).forEach(request =>
                        this.requestMapping.push({
                            "request": request,
                            "operation": this.assistant.RequestMapping[request]
                        })
                    );
                }
            });

            if (!window.ws || window.ws.readyState != WebSocket.OPEN)
                setTimeout(this.refreshData, 3000);
        },

        showEdit: function (name, operation) {
            this.edit.name = name;
            this.edit.newName = name;
            this.edit.operation = operation;
            this.edit.args = null;
            this.message = "";
        },
        showAddOperation: function () {
            this.edit.name = "Add Operation";
            this.edit.newName = "Add Operation";
            this.edit.operation = "";
            this.edit.args = null;
            this.message = "";
        },

        triggerOperation: function () {
            if (this.edit.name == "Add Operation")
                return;

            // save and then trigger
            this.saveOperation().done(() => {
                this.edit.name = this.edit.newName;
                callSystem("Assistant", "ExecuteOperation", this.edit.newName, this.edit.args)
                    .done(responseText => this.message = responseText || "Error!")
                    .fail(response => this.message = "Error: " + response.responseText);
            });
        },
        saveOperation: function () {
            if (this.edit.name == "Add Operation") {
                if (this.edit.newName == "") {
                    this.message = "Error: Cannot set operation without name";
                    return;
                }
                if (Object.keys(this.assistant.Operations).some(o => o == this.edit.newName)) {
                    this.message = "Error: An operation with the same name already exists";
                    return;
                }
            }

            if (this.edit.name != this.edit.newName)
                delete this.assistant.Operations[this.edit.name];
            this.assistant.Operations[this.edit.newName] = this.edit.operation;
            return setSystem("AssistantSystem", { "Operations": this.assistant.Operations })
                .done(() => this.edit.name = null)
                .fail(response => this.message = "Error: " + response.responseText);
        },
        deleteOperation: function () {
            if (!confirm("Are you sure you want to delete the operation?"))
                return;

            delete this.assistant.Operations[this.edit.name];
            return setSystem("AssistantSystem", { "Operations": this.assistant.Operations })
                .done(() => this.edit.name = null)
                .fail(response => this.message = "Error: " + response.responseText);
        },

        addArgumentMapping: function () {
            if (!this.argumentMapping.some(rm => rm.argument == ""))
                this.argumentMapping.push({ "argument": "", "value": "" });
        },
        saveArgumentMapping: function () {
            this.assistant.ArgumentMapping = this.argumentMapping
                .filter(rm => rm.argument != "")
                .reduce((obj, item) => Object.assign(obj, { [item.argument]: item.value }), {});

            this.argumentMapping = null;
            return setSystem("AssistantSystem", { "ArgumentMapping": this.assistant.ArgumentMapping })
                .done(() => this.message = "Saved!")
                .fail(response => this.message = "Error: " + response.responseText);
        },

        addRequestMapping: function () {
            if (!this.requestMapping.some(rm => rm.request == ""))
                this.requestMapping.push({ "request": "", "operation": "" });
        },
        saveRequestMapping: function () {
            this.assistant.RequestMapping = this.requestMapping
                .filter(rm => rm.request != "")
                .reduce((obj, item) => Object.assign(obj, { [item.request]: item.operation }), {});

            this.requestMapping = null;
            return setSystem("AssistantSystem", { "RequestMapping": this.assistant.RequestMapping })
                .done(() => this.message = "Saved!")
                .fail(response => this.message = "Error: " + response.responseText);
        }
    },
    mounted: function () {
        this.refreshData();
        window.ws?.addRefreshHandlers(this.refreshData);
    },
    unmounted: function () {
        window.ws?.removeRefreshHandlers(this.refreshData);
    },
    watch: {
        message: function () {
            if (this.message)
                setTimeout(() => this.message = "", 3000); // hide message after 3 seconds
        }
    }
});