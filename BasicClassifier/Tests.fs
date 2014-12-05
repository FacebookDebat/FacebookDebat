module Tests
open BasicClassifier
open NUnit.Framework

[<Test>]
let ``When I Score Hello mom,I expect a number between -100 and 100``() =
    let c1 = new Classifier("..\..\Nielsen2010.txt")
    let result = c1.Score "Hello mom"
    Assert.Less(result,100);
    Assert.Greater(result,-100);

[<Test>]
let ``When it add to numbers, I expect a number``()=
    let result = "fail"
    Assert.IsInstanceOf(int.GetType(),result)


