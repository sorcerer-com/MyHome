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
                debounceSetColor: debounce(this.setColor, 1000), // set only after no changes in 1 sec
                modalObject: null
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

            setColor: function () {
                setDevice(this.room.Name, this.driver.Name, { "Color": this.driver.Color })
                    .done(() => this.processing = false)
                    .fail(() => this.processing = false);
                this.processing = true;
            },

            save: function () {
                let obj = this.modalObject;
                this.modalObject = null;
                this.processing = true;
                return setDevice(this.room.Name, this.driver.Name, obj)
                    .done(() => this.processing = false)
                    .fail(() => this.processing = false);
            }
        },
        watch: {
            "driver.Color": function () {
                if (/Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent))
                    this.setColor(); // don't debounce for mobile version
                else
                    this.debounceSetColor();
            }
        }
    });
});