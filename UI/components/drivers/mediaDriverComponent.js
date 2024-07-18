setComponent("media-driver", {
    props: ["room", "driver", "hideName"],
    data: function () {
        return {
            processing: false,
            error: null,
            showModal: false,
            selectedMedia: ""
        }
    },
    computed: {
        isOffline: function () {
            return (new Date() - new Date(this.driver.LastOnline)) > 60 * 60 * 1000; // if online before more than 1 hour in millis
        },
        timeDetails: function () {
            function msToTime(s) {
                let pad = (n, z = 2) => ('00' + n).slice(-z);
                return pad(s / 3.6e6 | 0) + ':' + pad((s % 3.6e6) / 6e4 | 0); // + ':' + pad((s % 6e4) / 1000 | 0);
            }
            let time = msToTime(this.driver.Time);
            let length = msToTime(this.driver.Length);
            return `${time} / ${length}`;
        },
        position: function () {
            return this.driver.Time / this.driver.Length * 100;
        }
    },
    methods: {
        dateToString: dateToString,
        lastIndexOfPathSeparator: function (path) {
            return Math.max(path.lastIndexOf("\\"), path.lastIndexOf("/"));
        },

        getMediaTree: function () {
            let tree = {};
            for (let media of Object.keys(this.driver.MediaList)) {
                for (let path of this.driver.MediaList[media]) {
                    let split = path.split(/\\|\/|:/).filter(e => e); // split by \\, / and :
                    split.unshift(media); // add item to the start of the array
                    let currItem = tree;
                    for (let s of split) {
                        if (!(s in currItem))
                            currItem[s] = {};
                        currItem = currItem[s];
                    }
                    currItem["path"] = path;
                    currItem["marked"] = this.driver.Watched.includes(media + path);
                }
            }
            return tree;
        },
        onMediaChange: function (media) {
            if (this.selectedMedia == media) {
                this.callDriver('Play', media);
                this.showModal = false;
            }
            else
                this.selectedMedia = media;
        },

        click: function () {
            if (this.processing || this.driver.Playing)
                return;

            this.showModal = true;
            this.selectedMedia = "";
        },

        callDriver: function (funcName, ...args) {
            callDevice(this.room.Name, this.driver.Name, funcName, args)
                .done(() => this.processing = false)
                .fail(() => this.processing = false);
            this.processing = true;
        },
        sortByDate: function (value) {
            setDevice(this.room.Name, this.driver.Name, { "SortByDate": value });
        }
    },
    mounted: function () {
        if (this.$route.query.room == this.room.Name && this.$route.query.driver == this.driver.Name)
            this.showModal = true;
    },
    watch: {
        "showModal": function () {
            if (this.showModal)
                this.$router.push({ query: { room: this.room.Name, driver: this.driver.Name } });
            else
                this.$router.push({ query: {} });
        }
    }
});