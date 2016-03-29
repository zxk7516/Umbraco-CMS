(function() {
    'use strict';

    function variationsHelper() {

        var variations = [];

        /*
        var variations = [
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
        */

        function setMaster(master) {
            variations = [];
            master.master = true;
            variations.push(master);
        }

        function getVariations() {
            return variations;
        }

        function createVariation(variation) {
            var newVariation = angular.copy(variation);
            newVariation.master = false;
            variations.push(newVariation);
        }

        function saveVariation(updatedVariation) {

            for(var i = 0; i < variations.length; i++) {

                var variation = variations[i];

                if(variation.id === updatedVariation) {
                    variation.name = updatedVariation.name;
                    variation.description = updatedVariation.description;
                }
            }

        }

        function cloneVariation(variation) {
            var variationClone = angular.copy(variation);
            variationClone.master = false;
            variationClone.published = false;
            variations.push(variationClone);
        }

        function deleteVariation(variation, event, index) {
            variations.splice(index, 1);
        }

        var service = {
            setMaster: setMaster,
            getVariations: getVariations,
            createVariation: createVariation,
            saveVariation: saveVariation,
            cloneVariation: cloneVariation,
            deleteVariation: deleteVariation
        };

        return service;

    }


    angular.module('umbraco.services').factory('variationsHelper', variationsHelper);

})();
