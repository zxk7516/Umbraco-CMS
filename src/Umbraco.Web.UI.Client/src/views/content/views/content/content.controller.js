(function() {
    'use strict';

    function ContentController($scope) {

        var vm = this;

        vm.showGenericProperties = false;
        vm.genericProperties = {};

        vm.sidebarTabs = [{
            active: true,
            id: 1,
            label: "Details",
            alias: "details"
        }, {
            active: false,
            id: 2,
            label: "Activity",
            alias: "activity"
        }];

        vm.toggleGenericProperties = toggleGenericProperties;

        function toggleGenericProperties() {
            if(vm.showGenericProperties === true) {
                vm.showGenericProperties = false;
            } else {
                vm.showGenericProperties = true;
            }
        }

        function setGenericProperties(content) {
            for(var i = 0; i < content.tabs.length; i++) {
                var tab = content.tabs[i];
                if(tab.alias === "Generic properties") {
                    vm.genericProperties = tab;
                }
            }
        }

        setGenericProperties($scope.model);

    }

    angular.module("umbraco").controller("Umbraco.Editors.Content.ContentController", ContentController);
})();
