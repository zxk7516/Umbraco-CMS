LazyLoad.js(
    [
        'lib/jquery/jquery-2.0.3.min.js',

        /* the jquery ui elements we need */
        'lib/jquery/jquery-ui-1.10.3.custom.min.js',
        
        /* 1.2 */
        'lib/angular/1.2/angular.min.js',
        'lib/angular/1.2/angular-animate.min.js',
        'lib/angular/1.2/angular-cookies.min.js',
        'lib/angular/1.2/angular-touch.min.js',
        'lib/angular/1.2/angular-mocks.js',
        'lib/angular/1.2/angular-sanitize.min.js',
        'lib/angular/1.2/angular-route.min.js',
        
        /* App-wide file-upload helper */
        'lib/jquery/jquery.upload/js/jquery.fileupload.js',
        'lib/jquery/jquery.upload/js/jquery.fileupload-process.js',
        'lib/jquery/jquery.upload/js/jquery.fileupload-angular.js',
        
        'lib/bootstrap/js/bootstrap.2.3.2.min.js',
        'lib/lodash/lodash.min.js',
        'lib/umbraco/Extensions.js',
        'lib/umbraco/NamespaceManager.js',

        'js/umbraco.servervariables.js',
        'js/app.dev.js',
        'js/umbraco.httpbackend.js',
        'js/umbraco.testing.js',

        'js/umbraco.directives.js',
        'js/umbraco.filters.js',
        'js/umbraco.resources.js',
        'js/umbraco.services.js',
        'js/umbraco.security.js',
        'js/umbraco.controllers.js',
        'js/routes.js',
        'js/init.js'
  ],

  function () {
    jQuery(document).ready(function () {
        angular.bootstrap(document, ['umbraco']);
    });
  }
);