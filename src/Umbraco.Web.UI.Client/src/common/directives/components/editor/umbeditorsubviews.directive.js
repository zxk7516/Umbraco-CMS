(function() {
   'use strict';

   function EditorSubViewsDirective() {

      var directive = {
         restrict: 'E',
         replace: true,
         templateUrl: 'views/components/editor/umb-editor-sub-views.html',
         scope: {
            subViews: "=",
            model: "="
         }
      };

      return directive;
   }

   angular.module('umbraco.directives').directive('umbEditorSubViews', EditorSubViewsDirective);

})();
