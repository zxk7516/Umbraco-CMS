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
        }
    ];

        vm.toggleGenericProperties = toggleGenericProperties;

        function activate() {
            setGenericProperties($scope.model.tabs);
            hideGenericPropertiesTab($scope.model.tabs);
        }

        function hideGenericPropertiesTab(tabs) {

            for(var i = 0; i < tabs.length; i++) {
                var tab = tabs[i];
                if(tab.alias === "Generic properties") {
                    tab.hidden = true;
                }
            }
        }

        function toggleGenericProperties() {
            if(vm.showGenericProperties === true) {
                vm.showGenericProperties = false;
            } else {
                vm.showGenericProperties = true;
            }
        }

        function setGenericProperties(tabs) {
            for(var i = 0; i < tabs.length; i++) {
                var tab = tabs[i];
                if(tab.alias === "Generic properties") {
                    vm.genericProperties = tab;
                }
            }
        }

        activate();

    }

    angular.module("umbraco").controller("Umbraco.Editors.Content.ContentController", ContentController);
})();
