(function() {
   'use strict';

   function EditorSubViewSidebar() {

      var directive = {
         transclude: true,
         restrict: 'E',
         replace: true,
         templateUrl: 'views/components/editor/subview/umb-editor-sub-view-sidebar.html'
      };

      return directive;
   }

   angular.module('umbraco.directives').directive('umbEditorSubViewSidebar', EditorSubViewSidebar);

})();
