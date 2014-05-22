function segmentDashboardController($scope, umbRequestHelper, $log, $http, formHelper) {

    $scope.providers = [];
    $scope.configuring = false;
    $scope.config = {
        providerName: "",
        providerTypeName: "",
        values: []        
    };

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

    $scope.showConfig = function (item) {
        
        $scope.configuring = true;
        $scope.config.providerName = item.displayName;
        $scope.config.providerTypeName = item.typeName;
        $scope.config.values = item.config;
    }

    $scope.cancel = function () {

        formHelper.resetForm({ scope: $scope });

        $scope.configuring = false;
        $scope.config = {
            providerName: "",
            providerTypeName: "",
            values: []
        };
    }

    $scope.addItem = function() {
        $scope.config.values.push({ matchExpression: "", key: "", value: "" });
    }

    $scope.removeItem = function(item) {
        $scope.config.values = _.reject($scope.config.values, function(i) { return i === item; });
    }

    $scope.save = function () {
        if (formHelper.submitForm({ scope: $scope })) {
            umbRequestHelper.resourcePromise(
                $http.post(
                    umbRequestHelper.getApiUrl("segmentDashboardApiBaseUrl", "PostSaveProviderConfig",
                    [{ typeName: $scope.config.providerTypeName }]),
                    $scope.config.values),
                'Failed to save segment configuration')
            .then(function () {

                //now that its successful, save the persisted values back to the provider's values
                var provider = _.find($scope.providers, function (i) { return i.typeName === $scope.config.providerTypeName; });
                provider.config = $scope.config.values;

                //exit editing config
                $scope.cancel();
            });
        }
    }

    umbRequestHelper.resourcePromise(
            $http.get(umbRequestHelper.getApiUrl("segmentDashboardApiBaseUrl", "GetProviders")),
            "Failed to retrieve provider data")
        .then(function(data) {
            $scope.providers = data;
        });

}
angular.module("umbraco").controller("Umbraco.Dashboard.SegmentDashboard", segmentDashboardController);