(function() {
    'use strict';

    function variationsHelper() {

        var variations = [
            {
                "name": "EN",
                "description": "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Pellentesque pulvinar ornare risus",
                "published": false
            },
            {
                "name": "DA",
                "description": " Etiam at erat vitae risus sagittis porta quis vitae ex. Suspendisse finibus tellus nec purus convallis ullamcorper. Nullam finibus pharetra leo.",
                "published": false
            }
        ];

        function getVariations() {
            return variations;
        }

        function createVariation(variation) {
            variations.push(variation);
        }

        function cloneVariation(variation, event, index) {
            var variationCopy = angular.copy(variation);
            variationCopy.name = variationCopy.name + " Copy";
            variationCopy.editMode = true;
            variations.splice(index+1, 0, variationCopy);
        }

        function deleteVariation(variation, event, index) {
            variations.splice(index, 1);
        }

        var service = {
            getVariations: getVariations,
            createVariation: createVariation,
            cloneVariation: cloneVariation,
            deleteVariation: deleteVariation
        };

        return service;

    }


    angular.module('umbraco.services').factory('variationsHelper', variationsHelper);

})();
