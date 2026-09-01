setComponent("speaker-driver", {
    props: ["room", "driver", "hideName"],
    data: function () {
        return {
            processing: false,
            error: null,
            showModal: false,
            filter: "",
            songUrl: "",
            edit: null,
            renamePlaylist: null,
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
                songs.unshift({ Name: "Downloading...", Url: "Downloading...", Rating: 0, Exists: false })
            // filter by active playlist
            if (this.driver.ActivePlaylist && !this.edit) {
                const pl = this.driver.Playlists?.find(p => p.Name == this.driver.ActivePlaylist);
                if (pl)
                    songs = songs.filter(s => s.Name in pl.Songs).map(s => ({ ...s, Rating: pl.Songs[s.Name] }));
            }
            return songs.filter(s => s.Name.toLowerCase().includes(this.filter.toLowerCase()));
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
            this.processing = true;
            return setDevice(this.room.Name, this.driver.Name, value)
                .done(() => this.processing = false)
                .fail(() => this.processing = false);
        },
        callDevice: function (funcName, ...args) {
            this.processing = true;
            return callDevice(this.room.Name, this.driver.Name, funcName, ...args)
                .done(() => this.processing = false)
                .fail(() => this.processing = false);
        },

        addSong: function (event) {
            callDevice(this.room.Name, this.driver.Name, 'AddSong', [this.songUrl])
                .done(() => this.songUrl = "")
                .fail(() => this.songUrl = "");
            this.songUrl = null;
            event.target.parentElement.open = false;
        },
        renameSong: function () {
            let song = this.driver.Songs.find(s => s.Url == this.edit.Url);
            if (song) {
                callDevice(this.room.Name, this.driver.Name, "RenameSong", song.Name, this.edit.Name);
                this.edit = null;
            }
        },
        changeSongUrl: function () {
            let song = this.driver.Songs.find(s => s.Url == this.edit.Url);
            if (song) {
                callDevice(this.room.Name, this.driver.Name, "ChangeSongUrl", song.Name, this.edit.newUrl);
                this.edit = null;
            }
        },
        deleteSong: function (song, keepEntry) {
            if (!keepEntry && !confirm("Are you sure you want to delete the song?"))
                return;

            callDevice(this.room.Name, this.driver.Name, "DeleteSong", song.Name, keepEntry);
            if (!keepEntry)
                this.edit = null;
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
        },

        selectPlaylist: function (name) {
            this.driver.ActivePlaylist = name;
            this.setDevice({ 'ActivePlaylist': name });
            this.$forceUpdate();
        },
        createPlaylist: function (name) {
            if (!name)
                return;

            this.callDevice('CreatePlaylist', name).done(() => {
                this.setDevice({ 'ActivePlaylist': name });
                this.driver.Playlists.push({ Name: name, Songs: {} });
                this.driver.ActivePlaylist = name;
            });
        },
        deletePlaylist: function (name) {
            if (!confirm(`Are you sure you want to delete playlist "${name}"?`))
                return;

            this.callDevice('DeletePlaylist', name).done(() => {
                const index = this.driver.Playlists.indexOf(name);
                this.driver.Playlists.splice(index, 1);
            });
        },
        startRenamePlaylist: function (playlist) {
            this.renamePlaylist = { ...playlist };
            this.renamePlaylist.newName = this.renamePlaylist.Name;
        },
        confirmRenamePlaylist: function () {
            if (!this.renamePlaylist)
                return;

            this.callDevice('RenamePlaylist', this.renamePlaylist.Name, this.renamePlaylist.newName).done(() => {
                p = this.driver.Playlists.find(p => p.Name == this.renamePlaylist.Name);
                p.Name = this.renamePlaylist.newName;
                if (this.driver.ActivePlaylist == this.renamePlaylist.Name)
                    this.driver.ActivePlaylist = this.renamePlaylist.newName;
                this.renamePlaylist = null;
            });
        },
        isSongInActivePlaylist: function (song) {
            if (!this.driver.ActivePlaylist)
                return true;

            const pl = this.driver.Playlists?.find(p => p.Name == this.driver.ActivePlaylist);
            return pl ? (song.Name in pl.Songs) : false;
        },
        toggleSongInPlaylist: function (song) {
            if (!this.driver.ActivePlaylist)
                return;

            const pl = this.driver.Playlists?.find(p => p.Name == this.driver.ActivePlaylist);
            if (!this.isSongInActivePlaylist(song)) {
                this.callDevice('AddSongToPlaylist', this.driver.ActivePlaylist, song.Name)
                    .done(() => pl.Songs[song.Name] = 0);
            }
            else {
                this.callDevice('RemoveSongFromPlaylist', this.driver.ActivePlaylist, song.Name)
                    .done(() => delete pl.Songs[song.Name])
            }
        },
    },
    mounted: function () {
        if (this.$route.query.room == this.room.Name && this.$route.query.driver == this.driver.Name)
            this.showModal = true;
        this.filter = "";
    },
    watch: {
        "showModal": function () {
            if (this.showModal)
                this.$router.push({ query: { room: this.room.Name, driver: this.driver.Name } });
            else
                this.$router.push({ query: {} });
        },
        "driver.Volume": function () {
            if (window.vue.isMobile)
                this.setDevice({ 'Volume': this.driver.Volume }); // don't debounce for mobile version
            else
                this.debounceSetVolume({ 'Volume': this.driver.Volume });
        }
    }
});