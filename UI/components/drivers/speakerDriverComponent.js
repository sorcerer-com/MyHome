setComponent("speaker-driver", {
    props: ["room", "driver"],
    data: function () {
        return {
            processing: false,
            error: null,
            showModal: false,
            songUrl: "",
            debounceSetVolume: debounce(this.setDevice, 1000), // set only after no changes in 1 sec
        }
    },
    computed: {
        isOffline: function () {
            return (new Date() - new Date(this.driver.LastOnline)) > 60 * 60 * 1000; // if online before more than 1 hour in millis
        },
        songs: function () {
            let songs = [...this.driver.Songs];
            songs.sort((a, b) => a.Rating - b.Rating);
            songs.reverse();
            if (this.songUrl == null) // add dummy item while downloading
                songs.unshift({ Name: "Downloading...", Rating: 0, Exists: true })
            return songs;
        }
    },
    methods: {
        dateToString: dateToString,

        click: function () {
            if (this.processing)
                return;

            this.showModal = true;
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

        addSong: function (event) {
            callDevice(this.room.Name, this.driver.Name, 'AddSong', [this.songUrl])
                .done(() => this.songUrl = "")
                .fail(() => this.songUrl = "");
            this.songUrl = null;
            event.target.parentElement.open = false;
        },
        getQueueIndex: function (song) {
            let songIdx = this.driver.Songs.indexOf(song);
            return this.driver.Queue.indexOf(songIdx);
        },
        enqueue: function (song) {
            let songIdx = this.driver.Songs.indexOf(song);
            this.driver.Queue.push(songIdx)
            this.setDevice({ 'Queue': this.driver.Queue });
        },
        dequeue: function (song) {
            const index = this.getQueueIndex(song);
            this.driver.Queue.splice(index, 1);
            this.setDevice({ 'Queue': this.driver.Queue });
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
                this.$router.push("/");
        },
        "driver.Volume": function () {
            if (window.vue.isMobile)
                this.setDevice({ 'Volume': this.driver.Volume }); // don't debounce for mobile version
            else
                this.debounceSetVolume({ 'Volume': this.driver.Volume });
        }
    }
});