var scriptSrc = document.currentScript.src;
var templateUrl = scriptSrc.substr(0, scriptSrc.lastIndexOf(".")) + ".html";
$.get(templateUrl, template => {
    Vue.component("monaco-editor", {
        template: template,
        props: ["value", "type", "schema"],
        data: function () {
            return {
                editor: null
            }
        },
        methods: {
        },
        mounted: function () {
            if (this.type == null || this.type == "typescript") {
                monaco.languages.typescript.javascriptDefaults.setEagerModelSync(true);
                monaco.languages.typescript.typescriptDefaults.setCompilerOptions(
                    {
                        noLib: true,
                        allowNonTsExtensions: true
                    });

                monaco.editor.getModels().forEach(model => model.dispose()); // clear the models
                getTypescriptModels().done(models => monaco.editor.createModel(models, "typescript"));
            }
            else if (this.type == "json") {
                monaco.languages.json.jsonDefaults.setDiagnosticsOptions({
                    schemas: this.schema ? [{ fileMatch: ["*"], schema: this.schema }] : []
                });
            }

            // create editor
            var h_div = document.getElementById('monaco_editor');
            this.editor = monaco.editor.create(h_div, {
                value: this.value,
                language: this.type ?? 'typescript',
                contextmenu: false,
                lineNumbers: true,
                lineNumbersMinChars: 3,
                lineDecorationsWidth: 0,
                minimap: { enabled: false }
            });
            this.editor.getModel().updateOptions({ tabSize: 2 });
            this.editor.getModel().onDidChangeContent(_ => {
                // to work the v-model binding
                this.$emit("input", this.editor.getModel().getValue(monaco.editor.EndOfLinePreference.LF));
            });
        }
    });
});