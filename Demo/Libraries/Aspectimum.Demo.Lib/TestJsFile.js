// ##advice={ template: "JsFunctionBefore", nameFilter: "[a-z]{2,}" }
// ##advice={ template: "JsFunctionAfter", nameFilter: "[a-z]{2,}" }

// ##aspect="TestJavaScript" extra data here


function test()
{
    console.log('test1');
}


function test2() {
    console.log('test2.1');

    setTimeout(function () {
        console.log('test2.2')
    }, 0);
}


test();
test2();