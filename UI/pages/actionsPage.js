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
                    types: null,
                    object: null,
                    onSave: null,
                    onDelete: null
                }
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
                return Object.keys(this.actions.Actions).reduce((rv, x) => {
                    let action = this.actions.Actions[x];
                    rv[action.RoomName] = rv[action.RoomName] || {}
                    rv[action.RoomName][x] = action;
                    return rv;
                }, {});
            },

            showEdit: function (name, action, onSave, onDelete) {
                this.edit.name = name;
                this.edit.types = null;
                this.edit.object = action;
                this.edit.onSave = onSave;
                this.edit.onDelete = onDelete;
            },
            showAddAction: function () {
                getSubTypes("BaseAction").done(types => {
                    this.edit.name = "Add Action";
                    this.edit.types = types;
                    this.edit.object = null;
                    this.edit.onSave = null;
                    this.edit.onDelete = null;
                });
            },
            onTypeChange: function (event) {
                createAction(event.target.value).done(action => {
                    action.Name = "New Action";
                    action["$subtypes"]["Name"] = "String";
                    this.edit.object = action;
                    this.edit.onSave = this.saveAction;
                });
            },

            saveAction: function (action) {
                let name = this.edit.name;
                if (this.edit.name == "Add Action") {
                    if (action.Name == "")
                        return $.Deferred().reject({ responseText: "Cannot set action without name" });
                    if (Object.keys(this.actions.Actions).some(name => name == action.Name))
                        return $.Deferred().reject({ responseText: "An action with the same name already exists" });
                    name = action.Name;
                }

                if (action.RoomName == "")
                    return $.Deferred().reject({ responseText: "Cannot set action without room" });

                return setAction(name, action).done(() => this.edit.name = null);
            },
            deleteAction: function () {
                if (!confirm("Are you sure you want to delete the action?"))
                    return;

                deleteAction(this.edit.name).done(() => this.edit.name = null);
            },
        },
        mounted: function () {
            this.refreshData();
        }
    });
});