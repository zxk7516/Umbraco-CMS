/*globals jasmine*/

describe("grid 2", function () {

    var controller,
        rootScope,
        scope,
        gridResource,
        angularHelper,
        q,
        // These are populated at the bottom of this file
        gridEditors,
        rteScaffold,
        headerScaffold,
        scaffolds,
        fullModel;

    beforeEach(module('umbraco'));

    beforeEach(inject(function (
        $rootScope,
        $controller,
        $q
    ) {
        q = $q;
        rootScope = $rootScope;
        scope = $rootScope.$new();

        scope.model = JSON.parse(JSON.stringify(fullModel));

        gridResource = {
            getGridContentTypes: function () {
                var def = q.defer();
                def.resolve(gridEditors);
                return def.promise;
            },
            getScaffold: function (uuid) {
                var def = q.defer();
                def.resolve(scaffolds[uuid]);
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

        controller = $controller("Umbraco.PropertyEditors.Grid2Controller", {
            "$scope": scope,
            "gridResource": gridResource,
            "angularHelper": angularHelper
        });

        //fixme - Disable digest while we don't have the right logic
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
            scope.model.value.sections[0].rows[0].cells[0],
            0,
            ""
        );
        expect(scope.editorOverlay).toEqual(jasmine.objectContaining({
            view: "itempicker"
        }));
    });

    it("adds editor to cell",
        function () {
            scope.addControl(
                gridEditors[0],
                scope.model.value.sections[0].rows[0].cells[0],
                0
            );
            expect(scope.model.value.sections[0].rows[0].cells[0].items[2]).toEqual(jasmine.objectContaining({
                "type": "umb://document-type/9c620216c4f14f67b16468b103a09144",
                "values": {
                }
            }));
        });

    it("maps the model to persistable model",
        function () {
            //var persistable = scope.mapToPersistableModel(fullModel.value);

            scope.$emit("formSubmitting");
            var persisted = scope.model.value;

            expect(persisted).toEqual(jasmine.objectContaining({
                rows: [
                    {
                        alias: "Headline",
                        settings: {},
                        cells: [
                            {
                                "settings": {},
                                "items": [
                                    {
                                        "type": "umb://document-type/9c620216c4f14f67b16468b103a09144",
                                        "values": {
                                            "value": "Hello world!"
                                        }
                                    }, {
                                        "type": "umb://document-type/437c65f147054681911d8a884459ab47",
                                        "values": {
                                            "content": "<p>More hello world!</p>"
                                        }
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }));

        });

    gridEditors = [
        {
            "id": 1061,
            "udi": "umb://document-type/9c620216c4f14f67b16468b103a09144",
            "name": "Header",
            "alias": "header",
            "icon": "icon-document",
            "views": {
                "value": {
                    "view": "views/propertyeditors/textbox/textbox.inline.html",
                    "isPreview": false
                }
            }
        }, {
            "id": 1062,
            "udi": "umb://document-type/437c65f147054681911d8a884459ab47",
            "name": "RTE",
            "alias": "richText",
            "icon": "icon-document",
            "views": {
            }
        }
    ];

    rteScaffold = {
        "tabs": [
            {
                "id": 13,
                "active": true,
                "label": "Inline",
                "alias": "Inline",
                "properties": [
                    {
                        "label": "Content",
                        "description": null,
                        "view": "rte",
                        "config": {
                            "editor": null,
                            "hideLabel": false
                        },
                        "hideLabel": false,
                        "validation": {
                            "mandatory": false,
                            "pattern": null
                        },
                        "readonly": false,
                        "id": 0,
                        "value": null,
                        "alias": "content",
                        "editor": "Umbraco.TinyMCEv3",
                        "isSensitive": false
                    }
                ]
            }
        ],
        "updateDate": "0001-01-01T00:00:00",
        "createDate": "0001-01-01T00:00:00",
        "published": false,
        "edited": false,
        "owner": null,
        "updater": null,
        "contentTypeAlias": "richText",
        "sortOrder": 0,
        "name": null,
        "id": 0,
        "udi": "umb://document/437c65f147054681911d8a884459ab47",
        "icon": "icon-document",
        "trashed": false,
        "key": "437c65f1-4705-4681-911d-8a884459ab47",
        "parentId": -1,
        "alias": null,
        "path": null,
        "metaData": {}
    };

    headerScaffold = {
        "tabs": [
            {
                "id": 12,
                "active": true,
                "label": "Inline",
                "alias": "Inline",
                "properties": [
                    {
                        "label": "Value",
                        "description": null,
                        "view": "textbox",
                        "config": {
                            "maxChars": null
                        },
                        "hideLabel": false,
                        "validation": {
                            "mandatory": false,
                            "pattern": null
                        },
                        "readonly": false,
                        "id": 0,
                        "value": "",
                        "alias": "value",
                        "editor": "Umbraco.TextBox",
                        "isSensitive": false
                    }
                ]
            }
        ],
        "updateDate": "0001-01-01T00:00:00",
        "createDate": "0001-01-01T00:00:00",
        "published": false,
        "edited": false,
        "owner": null,
        "updater": null,
        "contentTypeAlias": "header",
        "sortOrder": 0,
        "name": null,
        "id": 0,
        "udi": "umb://document/9c620216c4f14f67b16468b103a09144",
        "icon": "icon-document",
        "trashed": false,
        "key": "1e03aad06a124e9e8092540709d3c1cf",
        "parentId": -1,
        "alias": null,
        "path": null,
        "metaData": {}
    };

    scaffolds = {
        "umb://document-type/437c65f147054681911d8a884459ab47": rteScaffold,
        "umb://document-type/9c620216c4f14f67b16468b103a09144": headerScaffold
    };

    fullModel = {
        "label": "Layout",
        "description": null,
        "view": "grid2",
        "config": {
             "items": {
                 "styles": [
                     {
                         "label": "Set a background image",
                         "description": "Set a row background",
                         "key": "background-image",
                         "view": "imagepicker",
                         "modifier": "url({0})"
                     }
                 ],
                 "config": [
                     {
                         "label": "Class",
                         "description": "Set a css class",
                         "key": "class",
                         "view": "textstring"
                     }
                 ],
                 "columns": 12,
                 "templates": [
                     {
                         "name": "1 column layout",
                         "sections": [
                             {
                                  "grid": 12
                             }
                         ]
                     }
                 ],
                 "layouts": [
                     {
                         "label": "Headline",
                         "name": "Headline",
                         "areas": [
                             {
                                 "grid": 12,
                                 "allowed": [
                                     "umb://document-type/437c65f147054681911d8a884459ab47",
                                     "umb://document-type/9c620216c4f14f67b16468b103a09144"
                                 ]
                             }
                         ]
                     }, {
                         "label": "FiftyFifty",
                         "name": "Article",
                         "areas": [
                             {
                                  "grid": 6
                             }, {
                                  "grid": 6
                             }
                         ]
                     }
                 ]
             }
        },
        "hideLabel": true,
        "validation": {
            "mandatory": false,
            "pattern": null
        },
        "readonly": false,
        "id": 0,
        "value": {
            "rows": [
                {
                    "alias": "Headline",
                    "settings": {},
                    "cells": [
                        {
                            "settings": {},
                            "items": [
                                {
                                    "type": "umb://document-type/9c620216c4f14f67b16468b103a09144",
                                    "values": {
                                         "value": "Hello world!"
                                    }
                                }, {
                                    "type": "umb://document-type/437c65f147054681911d8a884459ab47",
                                    "values": {
                                        "content": "<p>More hello world!</p>"
                                    }
                                }
                            ]
                        }]
                }]
        },
        "alias": "layout",
        "editor": "Umbraco.Grid2",
        "isSensitive": false
    };
});
