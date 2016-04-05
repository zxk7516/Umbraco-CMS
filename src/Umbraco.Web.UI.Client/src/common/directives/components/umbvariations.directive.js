(function() {
    'use strict';

    function Variations(iconHelper) {

        function link(scope, el, attr, ctrl) {

            scope.newVariation = {};

            scope.segments = [
                {
                    name: "Mobile",
                    id: 1
                },
                {
                    name: "Tablet",
                    id: 2
                },
                {
                    name: "Email Subscriber",
                    id: 3
                },
                {
                    name: "Email Campagin March 2016",
                    id: 4
                },
                {
                    name: "Codegarden Attendees",
                    id: 5
                },
                {
                    name: "Codegarden Attendees from abroad",
                    id: 6
                }
            ];

            scope.showNewVariation = function(language) {
                language.showNewVariation = true;
            };

            scope.hideNewVariation = function(language) {
                language.showNewVariation = false;
            };

            scope.toggleEditVariation = function(selectedVariation) {
                selectedVariation.editVariation = !selectedVariation.editVariation;
            };

            scope.createNewVariation = function(newVariation, language) {

                var variation = angular.copy(language);
                variation.variationName = newVariation.name;
                variation.variationDescription = newVariation.description;
                variation.segments = newVariation.segments;
                variation.active = false;

                language.variations.unshift(variation);

                language.showNewVariation = false;

                scope.newVariation = {
                    name: "",
                    description: "",
                    segments: []
                };

            };

            scope.saveVariation = function(variation, language) {
                variation.editVariation = false;
            };

            scope.deleteVariation = function(variation, language) {
                var index  = language.variations.indexOf(variation);
                language.variations.splice(index, 1);
            };

            scope.hideEditVariation = function(variation) {
                variation.editVariation = false;
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
                onCloneVariation: "=",
                onDeleteVariation: "="
            },
            link: link
        };

        return directive;
    }

    angular.module('umbraco.directives').directive('umbVariations', Variations);

})();
