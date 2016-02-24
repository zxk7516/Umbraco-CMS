(function() {
    'use strict';

    function Variations(iconHelper) {

        function link(scope, el, attr, ctrl) {

            scope.showNewVariation = function() {
                scope.newVariationIsVisible = true;
            };

            scope.hideNewVariation = function() {
                scope.newVariationIsVisible = false;
            };

            scope.createVariation = function(newVariation, event, index) {
                if(scope.onCreateVariation) {
                    scope.onCreateVariation(newVariation, event, index);
                    scope.newVariationIsVisible = false;
                }
            };

            scope.clickVariation = function(variation, event, index) {
                if(scope.onClickVariation) {
                    scope.onCreateVariation(variation, event, index);
                }
            };

            scope.editVariation = function(variation) {
                variation.editMode = true;
            };

            scope.closeVariationEditMode = function(variation) {
                variation.editMode = false;
            };

            scope.cloneVariation = function(variation, event, index) {
                if(scope.onCloneVariation) {
                    scope.onCloneVariation(variation, event, index);
                }
            };

            scope.deleteVariation = function(variation, event, index) {
                if(scope.onDeleteVariation) {
                    scope.onDeleteVariation(variation, event, index);
                }
            };

        }

        var directive = {
            restrict: 'E',
            replace: true,
            templateUrl: 'views/components/umb-variations.html',
            scope: {
                master: "=",
                variations: "=",
                onCreateVariation: "=",
                onCloneVariation: "=",
                onDeleteVariation: "="
            },
            link: link
        };

        return directive;
    }

    angular.module('umbraco.directives').directive('umbVariations', Variations);

})();
