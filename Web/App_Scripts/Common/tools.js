// http://stackoverflow.com/questions/1199352/smart-way-to-shorten-long-strings-with-javascript
String.prototype.trunc =
     function (n, useWordBoundary) {
         var toLong = this.length > n,
             s_ = toLong ? this.substr(0, n - 1) : this;
         s_ = useWordBoundary && toLong ? s_.substr(0, s_.lastIndexOf(' ')) : s_;
         return toLong ? s_ + '...' : s_;
     };

String.prototype.nl2br =
         function () {
             return this.replace(new RegExp('\r?\n', 'g'), '<br />');
         };

function sortByKey(array, key) {
    return array.sort(function (a, b) {
        var x = a[key]; var y = b[key];
        return ((x < y) ? -1 : ((x > y) ? 1 : 0));
    });
}

function sortByKeyDesc(array, key) {
    return array.sort(function (a, b) {
        var x = a[key]; var y = b[key];
        return ((x < y) ? 1 : ((x > y) ? -1 : 0));
    });
}




