var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("driver", {
        template: template,
        props: ["room", "driver"],
        data: function () {
            return {
                processing: false,
                error: null,
                debounceSetColor: debounce(this.setDevice, 1000), // set only after no changes in 1 sec
                modalObject: null
            }
        },
        computed: {
            isOffline: function () {
                return (new Date() - new Date(this.driver.LastOnline)) > 60 * 60 * 1000; // if online before more than 1 hour in millis
            }
        },
        methods: {
            getAcIcon: function () {
                switch (this.driver.Mode) {
                    case "AcMode.Off":
                        return "";
                    case "AcMode.Auto":
                        return "hdr_auto";
                    case "AcMode.Cool":
                        return "ac_unit";
                    case "AcMode.Heat":
                        return "wb_sunny";
                    case "AcMode.Dry":
                        return "invert_colors";
                    case "AcMode.Fan":
                        return "air";
                }
            },

            click: function () {
                if (this.processing)
                    return;
                if (this.driver.ConfirmationRequired &&
                    !confirm(`Are you sure you want to trigger the ${this.room.Name} ${this.driver.Name}?`))
                    return;

                if ("IsOn" in this.driver) {
                    this.driver.IsOn = !this.driver.IsOn;
                    setDevice(this.room.Name, this.driver.Name, { "IsOn": this.driver.IsOn })
                        .done(() => this.processing = false)
                        .fail(response => {
                            this.error = "Error: " + response.responseText
                            setTimeout(() => this.error = null, 3000);
                            this.processing = false;
                        });
                    this.processing = true;
                }

                if (this.modalObject == null &&
                    (this.driver.$type.endsWith("AcIrMqttDriver") ||
                        this.driver.$type.endsWith("SpeakerMqttDriver"))) {
                    this.modalObject = { ...this.driver };
                }
            },

            setDevice: function (value) {
                setDevice(this.room.Name, this.driver.Name, value)
                    .done(() => this.processing = false)
                    .fail(() => this.processing = false);
                this.processing = true;
            },
            callDevice: function (funcName, ...args) {
                callDevice(this.room.Name, this.driver.Name, funcName, args)
                    .done(() => this.processing = false)
                    .fail(() => this.processing = false);
                this.processing = true;
            },

            save: function () {
                let obj = filterObjectBySettings(this.modalObject, false);
                this.modalObject = null;
                this.processing = true;
                return setDevice(this.room.Name, this.driver.Name, obj)
                    .done(() => this.processing = false)
                    .fail(() => this.processing = false);
            }
        },
        watch: {
            "driver.Color": function () {
                if (window.vue.isMobile)
                    this.setDevice({ "Color": this.driver.Color }); // don't debounce for mobile version
                else
                    this.debounceSetColor({ "Color": this.driver.Color });
            }
        }
    });
});