(function() {
    'use strict';

    function VariationsController($scope, variationsHelper) {

        var vm = this;

        vm.variations = [];

        vm.clickVariation = clickVariation;
        vm.saveVariation = saveVariation;
        vm.cloneVariation = cloneVariation;
        vm.deleteVariation = deleteVariation;

        function activate() {
            variationsHelper.setMaster($scope.model);
            vm.variations = variationsHelper.getVariations();
        }

        function clickVariation(variation, event, index) {
            $scope.model = null;
            $scope.model = variation;

            for(var i = 0; i < $scope.subViews; i++) {
                var subView = scope.subViews[i];
                subView.active = false;
            }

            $scope.subViews[0].active = true;

        }

        function saveVariation(variation, event, index) {
            variationsHelper.saveVariation(variation);
        }

        function cloneVariation(variation, event, index) {
            variationsHelper.cloneVariation(variation, event, index);
        }

        function deleteVariation(variation, event, index) {
            variationsHelper.deleteVariation(variation, event, index);
        }

        activate();

    }

    angular.module("umbraco").controller("Umbraco.Editors.Content.VariationsController", VariationsController);
})();
