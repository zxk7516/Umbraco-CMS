(function() {
    'use strict';

    function Variations(iconHelper) {

        function link(scope, el, attr, ctrl) {

            scope.newVariation = {};

            scope.segments = [
                {
                    name: "Mobile"
                },
                {
                    name: "Front-end developer"
                },
                {
                    name: "back-end developer"
                }
            ];

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
                    published: true
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
                language.variations.unshift(newVariation);
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
