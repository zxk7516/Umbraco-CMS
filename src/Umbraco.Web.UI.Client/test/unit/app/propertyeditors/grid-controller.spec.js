describe("grid 2", function() {

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

    beforeEach(inject(function(
        $rootScope,
        $controller,
        $q
    ){
        q = $q;
        scope = $rootScope.$new();
        
        scope.model = {
            config: {
                items: {
                    config: {

                    },
                    templates: [
                        {
                            "name":"1 column layout",
                            "sections":[  
                                {  
                                    "grid":12
                                }
                            ]
                        }
                    ],
                    layouts: [  
                        {  
                            "name":"Full width",
                            "areas":[  
                                {  
                                    "grid":12,
                                    "allowAll":false,
                                    "allowed":[  
                                        "headline"
                                    ]
                                }
                            ]
                        }
                    ]
                }
            }
        };

        gridService = {
            getGridEditors: function() {
                var def = q.defer();
                def.resolve([]);
                return def.promise;
            }
        }

        angularHelper = {
            getCurrentForm: function() {
                return {
                    $setDirty: function() {
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

    it("defaults to 12 columns", function() {
        expect(scope.model.config.items.columns).toBe(12);
    });

    it("when only one layout and row config, adds layout and row", function() {
        expect(scope.model.value.sections[0].rows[0]).toBeDefined();
    });

    it("shows add editor dialog", function() {

    });

});