(function () {
    'use strict';

    function pendingController($scope) {
        var pvm = this;
    }

    angular.module('umbraco')
        .controller('deepLPendingController', pendingController);
})();