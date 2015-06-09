function nuCacheController($scope, umbRequestHelper, $log, $http, $q, $timeout) {

    $scope.reload = function () {
        if (confirm("Trigger a in-memory and local file cache reload on all servers.")) {
            $scope.reloading = true;
            umbRequestHelper.resourcePromise(
                $http.post(umbRequestHelper.getApiUrl("nuCacheStatusBaseUrl", "ReloadCache")),
                'Failed to trigger a cache reload')
            .then(function (result) {
                $scope.reloading = false;
            });
        }
    };

    $scope.reloading = false;

    function verify () {
        $scope.verifying = true;
        umbRequestHelper.resourcePromise(
                $http.get(umbRequestHelper.getApiUrl("nuCacheStatusBaseUrl", "VerifyDbCache")),
                'Failed to verify the cache.')
            .then(function (result) {
                $scope.verifying = false;
                $scope.invalid = result === "false";
            });
    };

    $scope.rebuild = function() {
        if (confirm("Rebuild cmsContentNu table content. Expensive.")) {
            $scope.rebuilding = true;
            $scope.verifying = true;
            umbRequestHelper.resourcePromise(
                    $http.post(umbRequestHelper.getApiUrl("nuCacheStatusBaseUrl", "RebuildDbCache")),
                    'Failed to rebuild the cache.')
                .then(function(result) {
                    $scope.rebuilding = false;
                    $scope.verifying = false;
                    $scope.invalid = result === "false";
                });
        }
    };

    $scope.rebuilding = false;

    verify();

}
angular.module("umbraco").controller("Umbraco.Dashboard.NuCacheController", nuCacheController);