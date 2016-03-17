(function() {
    'use strict';

    function variationsHelper() {

        var variations = [];

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
