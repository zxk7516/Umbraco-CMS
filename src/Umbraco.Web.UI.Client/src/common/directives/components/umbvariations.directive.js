(function() {
    'use strict';

    function Variations(iconHelper) {

        function link(scope, el, attr, ctrl) {

            scope.variations = [
                {
                    language: "Danish",
                    master: true,
                    published: true,
                    variations: [
                        {
                            name: "Mobile",
                            description: "Danish content for mobile",
                            published: true,
                            segments: [
                                {
                                    name: "Mobile"
                                }
                            ]
                        }
                    ]
                },
                {
                    language: "Dutch",
                    published: true,
                    variations: [
                        {
                            name: "Mobile",
                            description: "Dutch content for mobile",
                            segments: [
                                {
                                    name: "Mobile"
                                }
                            ]
                        }
                    ]
                },
                {
                    language: "English (United States)",
                    published: true,
                    variations: [
                        {
                            name: "Mobile",
                            description: "English content for mobile",
                            published: true,
                            segments: [
                                {
                                    name: "Mobile"
                                }
                            ]
                        },
                        {
                            name: "Job campaign: Front-end developer",
                            description: "Landing page for job thing 1",
                            published: true,
                            segments: [
                                {
                                    name: "Front-end developer"
                                }
                            ]
                        },
                        {
                            name: "Job campaign: Back-end developer",
                            description: "Landing page for job thing 2",
                            published: true,
                            segments: [
                                {
                                    name: "Back-end developer"
                                }
                            ]
                        }
                    ]
                },
                {
                    language: "German",
                    published: true
                },
                {
                    language: "Italian",
                    published: false
                },
                {
                    language: "Spanish",
                    published: false
                },
                {
                    language: "Swedish",
                    published: false
                }
            ];

            scope.showNewVariation = function() {
                scope.newVariationIsVisible = true;
            };

            scope.hideNewVariation = function() {
                scope.newVariationIsVisible = false;
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
                onCloneVariation: "=",
                onDeleteVariation: "="
            },
            link: link
        };

        return directive;
    }

    angular.module('umbraco.directives').directive('umbVariations', Variations);

})();
