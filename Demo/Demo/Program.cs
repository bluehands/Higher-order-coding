using System;
using DarkLink.AutoNotify;

namespace Demo
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var obj = new ClassA();
            obj.FieldA = 2;
            obj.FieldB = 4;

            var theGoodness = GoodEnum.Good;
            var theGoodness2 = theGoodness.Match(
                () => "good",
                () => "very good",
                () => "the goodest",
                () => "the very goodest");

            var schema = new Schema();

            var output = BF.HelloWorld();
        }
    }

    public enum GoodEnum
    {
        Good,
        VeryGood,
        TheGoodest,
        TheVeryGoodest,
    }

    public partial class ClassA
    {
        [AutoNotify]
        private int fieldA, fieldB;

        [AutoNotify(UsePrivateSetter = true)]
        private float fieldC;
    }
}