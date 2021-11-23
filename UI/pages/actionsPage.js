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
                error: ""
            }
        },
        methods: {
            refreshData: function () {
                if (this._isDestroyed)
                    return;

                getSystem("Actions", true).done(actions => {
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
                    let target = x.Executor?.Target?.substr(0, x.Executor?.Target?.lastIndexOf(" "))
                    let roomName = target?.split(".")[0];
                    rv[roomName] = rv[roomName] || {}
                    rv[roomName][x.Name] = x;
                    return rv;
                }, {});
            },

            showEdit: function (name, action) {
                getSubTypes("BaseExecutor").done(executorTypes => {
                    this.edit.name = name;
                    this.edit.action = action;
                    this.edit.types = { "executor": executorTypes };
                });
            },
            showAddAction: function () {
                $.when(getSubTypes("BaseAction"), getSubTypes("BaseExecutor"))
                    .done((actionTypes, executorTypes) => {
                        this.edit.name = "Add Action";
                        this.edit.action = null;
                        this.edit.types = { "action": actionTypes[0], "executor": executorTypes[0] };
                    });
            },
            onTypeChange: function (event) {
                createAction(event.target.value).done(action => {
                    action.Name = "New Action";
                    action["$subtypes"]["Name"] = "String";
                    this.edit.action = action;
                });
            },
            onExecutorTypeChange: function (event) {
                createActionExecutor(event.target.value).done(executor => {
                    this.edit.action.Executor = executor;
                });
            },

            saveAction: function () {
                let name = this.edit.name;
                if (this.edit.name == "Add Action") {
                    if (this.edit.action.Name == "") {
                        this.error = "Cannot set action without name";
                        return;
                    }
                    if (this.actions.Actions.some(a => a.Name == this.edit.action.Name)) {
                        this.error = "An action with the same name already exists";
                        return;
                    }
                    name = this.edit.action.Name;
                }

                if (this.edit.action.Executor == null) {
                    this.error = "Cannot set action without Executor";
                    return;
                }

                return setAction(name, this.edit.action).done(() => this.edit.name = null);
            },
            cloneAction: function () {
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
        }
    });
});