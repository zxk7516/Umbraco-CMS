(function() {
    'use strict';

    function WebsiteSelector() {

        function link(scope, el, attr, ctrl) {

            scope.showWebsiteDropdown = false;
            scope.selectedWebsite = {};

            function activate() {
                for(var i = 0; i < scope.websites.length; i++) {
                    var website = scope.websites[i];
                    if(website.selected) {
                        scope.selectedWebsite = website;
                    }
                }
            }

            scope.toggleWebsiteDropDown = function() {
                scope.showWebsiteDropdown = !scope.showWebsiteDropdown;
            };

            scope.closeWebsiteDropDown = function() {
                scope.showWebsiteDropdown = false;
            };

            scope.createWebsite = function() {
                if(scope.onCreateWebsite) {
                    scope.onCreateWebsite();
                    scope.closeWebsiteDropDown();
                }
            };

            scope.clickWebsite = function(website) {
                if(scope.onClickWebsite) {
                    scope.onClickWebsite(website);
                    setSelectedWebsite(website, scope.websites);
                    scope.closeWebsiteDropDown();
                }
            };

            function setSelectedWebsite(selectedWebsite, websites) {
                for(var i = 0; i < websites.length; i++) {
                    var website = websites[i];
                    website.selected = false;
                }
                selectedWebsite.selected = true;
                scope.selectedWebsite = selectedWebsite;
            }

            scope.$watch('websites', function(newValue, oldValue){
                activate();
            }, true);


        }

        var directive = {
            restrict: 'E',
            replace: true,
            templateUrl: 'views/components/umb-website-selector.html',
            link: link,
            scope: {
                websites: "=",
                onCreateWebsite: "=",
                onClickWebsite: "="
            }
        };

        return directive;
    }

    angular.module('umbraco.directives').directive('umbWebsiteSelector', WebsiteSelector);

})();
