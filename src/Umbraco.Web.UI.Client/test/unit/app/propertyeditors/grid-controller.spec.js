describe("grid 2", function() {

    var controller,
        scope,
        gridService,
        q;

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

                    }
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

        controller = $controller("Umbraco.PropertyEditors.GridController", {
            "$scope": scope,
            "gridService": gridService
        });

        scope.$digest();
    }));

    it("defaults to 12 columns", function() {
        expect(scope.model.config.items.columns).toBe(12);
    });

});