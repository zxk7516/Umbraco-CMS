describe("grid 2", function () {

    var controller,
        scope,
        gridService,
        angularHelper,
        q,
        gridEditors = [
            {
                "name": "Headline",
                "alias": "headline",
                "view": "textstring",
                "render": null,
                "icon": "icon-coin",
                "config": {
                    "style": "font-size: 36px; line-height: 45px; font-weight: bold",
                    "markup": "<h1>#value#</h1>"
                }
            }
        ];

    function outputModel() {
        console.log(JSON.stringify(scope.model, null, ' '));
    }

    beforeEach(module('umbraco'));

    beforeEach(inject(function (
        $rootScope,
        $controller,
        $q
    ) {
        q = $q;
        scope = $rootScope.$new();

        scope.model = {
            config: {
                columns: 12,
                rows: [
                    {
                        alias: "fullwidth",
                        Name: "Full width",
                        settingsType: "5C25DA30-822E-4E39-BDD5-1D86058323E3",
                        cells: [
                            {
                                colspan: 8,
                                // Main column settings
                                settingsType: "B994CB2F-D5A0-48DD-A8BA-AD8E4970B216",
                                allowAll: false,
                                allowed: [
                                    "84ADAEB2-BB42-4069-BCA8-52605158ECD2"
                                ]
                            },
                            {
                                colspan: 4,
                                // Sidebar settings
                                settingsType: "5DBC34A6-FEF5-4169-93BB-05CAB5344663",
                                allowAll: true,
                                allowed: []
                            }
                        ]
                    }
                ]
            },
            value: {
                rows: [
                    {
                        alias: "fullwidth",
                        settings: {
                            classNames: "fancy row",
                            backgroundImage: "0BAD7ABF-F423-4336-A6A4-F00AF1815971"
                        },
                        cells: [
                            {
                                settings: {
                                    type: "B994CB2F-D5A0-48DD-A8BA-AD8E4970B216",
                                    values: {
                                        classNames: "fancy cell",
                                        backgroundImage: "9805F5A9-D6F7-4981-88F7-2F2644ED0759"
                                    }
                                },
                                items: [
                                    {
                                        type: "84ADAEB2-BB42-4069-BCA8-52605158ECD2",
                                        values: {
                                            "headline": "Welcome to the fantastic site"
                                        }
                                    }
                                ]
                            },
                            {
                                settings: {
                                    type: "5DBC34A6-FEF5-4169-93BB-05CAB5344663",
                                    values: {
                                        classNames: "sidebar cell",
                                        sidebarSetting: "some other setting"
                                    }
                                },
                                items: [
                                    {
                                        type: "DBAD0C5C-95F2-4DF8-A888-C84B24DA1962",
                                        values: {
                                            "links": [
                                                "47873858-D92B-4D9F-90C0-04A6C14954FF",
                                                "F0814EF0-E356-44F3-911F-AF2A1B209223"
                                            ]
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        };

        gridService = {
            getGridEditors: function () {
                var def = q.defer();
                def.resolve([]);
                return def.promise;
            }
        }

        angularHelper = {
            getCurrentForm: function () {
                return {
                    $setDirty: function () {
                        var iRememberBeingDirty = true;
                    }
                }
            }
        }

        controller = $controller("Umbraco.PropertyEditors.GridController", {
            "$scope": scope,
            "gridService": gridService,
            "angularHelper": angularHelper
        });

        scope.$digest();
    }));

    it("defaults to 12 columns", function () {
        expect(scope.model.config.items.columns).toBe(12);
    });

    it("when only one layout and row config, adds layout and row", function () {
        expect(scope.model.value.sections[0].rows[0]).toBeDefined();
    });

    it("shows add editor dialog", function () {
        expect(scope.editorOverlay).toBeUndefined();
        scope.openEditorOverlay(
            {},
            scope.model.value.sections[0].rows[0].areas[0],
            0,
            ""
        );
        expect(scope.editorOverlay).toEqual(jasmine.objectContaining({
            view: "itempicker"
        }));
    });

    it("adds editor to cell", function () {
        scope.addControl(
            gridEditors[0],
            scope.model.value.sections[0].rows[0].areas[0],
            0
        );

        expect(scope.model.value.sections[0].rows[0].areas[0].controls[0]).toEqual(jasmine.objectContaining({
            value: null,
            editor: jasmine.objectContaining({
                alias: "headline"
            })
        }));
    })

});
