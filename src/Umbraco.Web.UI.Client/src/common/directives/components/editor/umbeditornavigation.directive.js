(function() {
   'use strict';

   function EditorNavigationDirective() {

      function link(scope, el, attr, ctrl) {

         scope.showNavigation = true;
         scope.showDropdown = false;
         scope.maxNavigationItems = 5;
         scope.overflowingNavigationsItems = 0;
         scope.needDropdown = false;

         scope.clickNavigationItem = function(selectedItem) {

            setItemToActive(selectedItem);

            runItemAction(selectedItem);

            if(scope.showDropdown) {
                scope.closeDropdown();
            }

         };

         function runItemAction(selectedItem) {
            if (selectedItem.action) {
               selectedItem.action(selectedItem);
            }
         }

         function setItemToActive(selectedItem) {
            // set all other views to inactive
            if (selectedItem.view) {

               for (var index = 0; index < scope.navigation.length; index++) {
                  var item = scope.navigation[index];
                  item.active = false;
               }

               // set view to active
               selectedItem.active = true;

            }
         }

         function activate() {

            // hide navigation if there is only 1 item
            if (scope.navigation.length <= 1) {
               scope.showNavigation = false;
            }

            hideOverflowingNavigationItems();

         }

         function hideOverflowingNavigationItems() {

             scope.totalNavigationItems = scope.navigation.length;

             if(scope.totalNavigationItems > scope.maxNavigationItems){
                 scope.needDropdown = true;
                 scope.overflowingNavigationsItems = scope.maxNavigationItems - scope.totalNavigationItems;
             }

         }

         scope.toggleDropdown = function() {
             scope.showDropdown = !scope.showDropdown;
         };

         scope.closeDropdown = function() {
             scope.showDropdown = false;
         };

         activate();

      }

      var directive = {
         restrict: 'E',
         replace: true,
         templateUrl: 'views/components/editor/umb-editor-navigation.html',
         scope: {
            navigation: "="
         },
         link: link
      };

      return directive;
   }

   angular.module('umbraco.directives.html').directive('umbEditorNavigation', EditorNavigationDirective);

})();
