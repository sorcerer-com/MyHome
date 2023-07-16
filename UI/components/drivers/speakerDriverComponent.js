var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    window.vue.component("speaker-driver", {
        template: template,
        props: ["room", "driver"],
        data: function () {
            return {
                processing: false,
                error: null,
                showModal: false,
                songUrl: ""
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
                this.callDevice('AddSong', this.songUrl);
                this.songUrl = "";
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
        }
    });
});