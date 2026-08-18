$(function () {
    $(".persian-date").persianDatepicker({
        format: 'YYYY/MM/DD',
        autoClose: true,
        initialValue: false,
        observer: true,
        calendar: {
            persian: {
                locale: 'fa'
            }
        }
    });
});