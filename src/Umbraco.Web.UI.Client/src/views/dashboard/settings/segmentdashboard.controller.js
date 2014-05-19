function segmentDashboardController($scope, umbRequestHelper, $log, $http) {

    $scope.providers = [];

    $scope.toggle = function(provider) {
        umbRequestHelper.resourcePromise(
                $http.post(
                    umbRequestHelper.getApiUrl("segmentDashboardApiBaseUrl", "PostToggleProvider",
                    [{ typeName: provider.typeName }])),
                'Failed to toggle segment provider')
            .then(function() {
                //toggle local var
                provider.enabled = !provider.enabled;
            });
    }

    umbRequestHelper.resourcePromise(
            $http.get(umbRequestHelper.getApiUrl("segmentDashboardApiBaseUrl", "GetProviders")),
            "Failed to retrieve provider data")
        .then(function(data) {
            $scope.providers = data;
        });

}
angular.module("umbraco").controller("Umbraco.Dashboard.SegmentDashboard", segmentDashboardController);