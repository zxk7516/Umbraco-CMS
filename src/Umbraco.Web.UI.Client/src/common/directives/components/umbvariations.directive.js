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
                    scope.cloneVariationIsVisible = false;
                }
            };

            scope.clickVariation = function(variation, event, index) {
                if(scope.onClickVariation && !variation.editMode) {
                    scope.onClickVariation(variation, event, index);
                }
            };

            scope.openEditVariation = function(variation, event, index) {
                scope.selectedVariation = variation;
                scope.selectedVariation.variatonNameCopy = angular.copy(variation.variatonNameCopy);
                scope.editVariationIsVisible = true;
                event.stopPropagation();
            };

            scope.hideEditVariation = function(event) {
                scope.editVariationIsVisible = false;
            };

            scope.saveVariation = function(variation, event, index) {
                if(scope.onSaveVariation) {
                    scope.onSaveVariation(variation, event, index);
                    scope.editVariationIsVisible = false;
                }
            };

            scope.openCloneVariation = function(variation, event) {
                scope.selectedVariation = angular.copy(variation);
                scope.selectedVariation.nameCopy = angular.copy(variation.name);
                scope.cloneVariationIsVisible = true;
                event.stopPropagation();
            };

            scope.hideCloneVariation = function() {
                scope.cloneVariationIsVisible = false;
            };

            scope.cloneVariation = function(variation) {
                if(scope.onCloneVariation) {
                    scope.onCloneVariation(variation);
                    scope.cloneVariationIsVisible = false;
                }
            };

            scope.deleteVariation = function(variation, event, index) {
                if(scope.onDeleteVariation) {
                    scope.onDeleteVariation(variation, event, index);
                    event.stopPropagation();
                }
            };

        }

        var directive = {
            restrict: 'E',
            replace: true,
            templateUrl: 'views/components/umb-variations.html',
            scope: {
                variations: "=",
                onClickVariation: "=",
                onSaveVariation: "=",
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
