var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("actions-page", {
        template: template,
        data: function () {
            return {
                actions: {},

                edit: {
                    name: null,
                    action: null,
                    types: null
                },
                message: ""
            }
        },
        methods: {
            refreshData: function () {
                if (this._isDestroyed)
                    return;

                getSystem("Actions").done(actions => {
                    Vue.set(this, "actions", actions);
                    setTimeout(this.refreshData, 3000);
                }).fail(() => {
                    setTimeout(this.refreshData, 1000);
                });
            },

            getGroupedActions: function () {
                if (!this.actions.Actions)
                    return {};

                // room name / action name / action
                return this.actions.Actions.reduce((rv, x) => {
                    let roomName = x.TargetRoomName;
                    rv[roomName] = rv[roomName] || {}
                    rv[roomName][x.Name] = x;
                    return rv;
                }, {});
            },

            showEdit: function (name, action) {
                this.message = "";
                this.edit.name = name;
                this.edit.action = action;
            },
            showAddAction: function () {
                this.message = "";
                getSubTypes("BaseAction").done(actionTypes => {
                    this.edit.name = "Add Action";
                    this.edit.action = null;
                    this.edit.types = actionTypes;
                });
            },
            onTypeChange: function (event) {
                createAction(event.target.value).done(action => {
                    action.Name = "New Action";
                    this.edit.action = action;
                });
            },

            triggerAction: function () {
                if (this.edit.name == "Add Action")
                    return;
                // save and then trigger
                setAction(this.edit.name, this.edit.action).done(() => {
                    triggerAction(this.edit.name)
                        .done(responseText => this.message = "Successfull! " + responseText)
                        .fail(response => this.message = "Error: " + response.responseText);
                });
            },
            saveAction: function () {
                let name = this.edit.name;
                if (this.edit.name == "Add Action") {
                    if (this.edit.action.Name == "") {
                        this.message = "Error: Cannot set action without name";
                        return;
                    }
                    if (this.actions.Actions.some(a => a.Name == this.edit.action.Name)) {
                        this.message = "Error: An action with the same name already exists";
                        return;
                    }
                    name = this.edit.action.Name;
                }

                return setAction(name, this.edit.action).done(() => this.edit.name = null);
            },
            cloneAction: function () {
                this.message = "";
                this.edit.name = this.edit.name + " - Clone";
                this.edit.action = { ...this.edit.action }; // clone the object
                this.edit.action.Name = this.edit.name;
            },
            deleteAction: function () {
                if (!confirm("Are you sure you want to delete the action?"))
                    return;

                deleteAction(this.edit.name).done(() => this.edit.name = null);
            },

            toggleActionEnabled: function (action) {
                action.IsEnabled = !action.IsEnabled;

                setAction(action.Name, action);
            }
        },
        mounted: function () {
            this.refreshData();
        },
        watch: {
            message: function () {
                if (this.message)
                    setTimeout(() => this.message = "", 3000); // hide message after 3 seconds
            }
        }
    });
});